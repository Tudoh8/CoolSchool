using CoolSchool.Aliens;
using CoolSchool.Battle;
using CoolSchool.Rewards;
using CoolSchool.Tag;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Adds the rewards to the playground scene: the star jar and the present popup on the touch UI,
    /// the 3D reveal stage far below the playground, the RewardSystem and the equipment on the player.
    /// Called once by the playground builder after the games exist.
    /// </summary>
    public static class RewardsSetupBuilder
    {
        const string RewardsFolder = "Assets/CoolSchool/Rewards";
        const string RenderTexturePath = RewardsFolder + "/Reveal.renderTexture";
        const string RevealLayerName = "RewardReveal";
        const int FallbackLayer = 31;
        const string CanvasName = "TouchUI";

        const float JarSize = 200f;
        const float JarMargin = 150f;
        const float BadgeSize = 130f;
        const float PresentSize = 600f;
        const float RevealSize = 820f;
        const float RaysSize = 1100f;
        const float MedalSize = 700f;
        const float CurrentCardSize = 200f;
        const float CurrentCardX = 560f;
        const float ArrowSize = 110f;
        const float SwapButtonSize = 220f;
        const float SwapButtonX = 330f;
        const float SwapButtonY = -400f;
        const int RevealTextureSize = 1024;

        /// <summary>Where the reveal stage sits: far below the playground, where the main camera never looks.</summary>
        static readonly Vector3 StagePosition = new Vector3(0f, -100f, 0f);

        public static void Setup(GameObject player)
        {
            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool", "Rewards");

            RewardCatalog catalog = RewardCatalogSeeder.CreateOrUpdate();
            int layer = EnsureLayer(RevealLayerName);
            if (layer < 0) layer = FallbackLayer;

            RevealStage stage = BuildRevealStage(layer);
            HideLayerFromMainCamera(layer);

            StarJarHud hud = null;
            PresentPopup popup = null;
            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas != null)
            {
                Sprite star = FindSprite("Star", "Stars/Star");
                Sprite gift = FindSprite("Gift", "Icons/Gift");
                hud = BuildStarJar(canvas.transform, star);
                BuildGoldenSwordBadge(canvas.transform);
                popup = BuildPresentPopup(canvas.transform, gift, star, stage, catalog);
            }
            else
            {
                Debug.LogWarning($"CoolSchool: no '{CanvasName}' canvas found, so the star jar and the present were not built.");
            }

            BuildRewardSystem(catalog, hud, popup);
            BuildGoldenSwordReward();

            // Sword, shield, hat and buddy: one applier for all of them (the hat holder and the buddy are made there too).
            EquipmentSetupBuilder.Setup(player, catalog);
        }

        // ----- 3D reveal stage -----------------------------------------------------------------

        static RevealStage BuildRevealStage(int layer)
        {
            var root = new GameObject("RevealStage");
            root.transform.position = StagePosition;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);

            var stage = root.AddComponent<RevealStage>();
            PlaygroundPrototypeBuilder.SetObject(stage, "pivot", pivot);
            PlaygroundPrototypeBuilder.SetInt(stage, "stageLayer", layer);
            PlaygroundPrototypeBuilder.SetObject(stage, "revealCamera", BuildRevealCamera(root.transform, layer));

            BuildStageLight("KeyLight", root.transform, new Vector3(-2f, 2f, -3f), new Color(1f, 0.95f, 0.85f), 3f);
            BuildStageLight("FillLight", root.transform, new Vector3(2.5f, 0.5f, -2.5f), new Color(0.75f, 0.85f, 1f), 1.5f);

            SetLayerRecursive(root, layer);
            return stage;
        }

        /// <summary>A camera that sees only the stage layer, close up, and films into the Reveal RenderTexture.</summary>
        static Camera BuildRevealCamera(Transform parent, int layer)
        {
            var cameraObject = new GameObject("RevealCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.15f, -5f);
            cameraObject.transform.localRotation = Quaternion.identity;

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Transparent. The soft purple only shows if a device drops the alpha channel.
            camera.backgroundColor = new Color(0.3f, 0.22f, 0.5f, 0f);
            camera.cullingMask = 1 << layer;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 12f;
            camera.depth = -10f;
            camera.allowHDR = false;
            camera.useOcclusionCulling = false;
            camera.targetTexture = GetOrCreateRenderTexture();
            camera.enabled = false;
            return camera;
        }

        static void BuildStageLight(string name, Transform parent, Vector3 localPosition, Color color, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 10f;
            light.shadows = LightShadows.None;
        }

        static RenderTexture GetOrCreateRenderTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (texture != null) return texture;

            texture = new RenderTexture(RevealTextureSize, RevealTextureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "Reveal",
                antiAliasing = 4,
            };
            AssetDatabase.CreateAsset(texture, RenderTexturePath);
            return AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        }

        static void HideLayerFromMainCamera(int layer)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                Debug.LogWarning("CoolSchool: no main camera found; the reveal stage layer is hidden from it at runtime instead.");
                return;
            }

            main.cullingMask &= ~(1 << layer);
            EditorUtility.SetDirty(main);
        }

        // ----- Touch UI ------------------------------------------------------------------------

        /// <summary>The star in the top-right corner: a dim empty star with a gold one that fills up on top of it.</summary>
        static StarJarHud BuildStarJar(Transform canvas, Sprite star)
        {
            RectTransform root = NewRect("StarJar", canvas);
            SetAnchored(root, new Vector2(1f, 1f), new Vector2(-JarMargin, -JarMargin), new Vector2(JarSize, JarSize));

            AddImage(NewStretched("Empty", root), star, new Color(0.25f, 0.25f, 0.35f, 0.6f));

            Image fill = AddImage(NewStretched("Fill", root), star, new Color(1f, 0.82f, 0.15f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;

            var hud = root.gameObject.AddComponent<StarJarHud>();
            PlaygroundPrototypeBuilder.SetObject(hud, "fill", fill);
            PlaygroundPrototypeBuilder.SetObject(hud, "target", root);
            PlaygroundPrototypeBuilder.SetObject(hud, "flyingStarSprite", star);
            return hud;
        }

        /// <summary>
        /// A small golden sword left of the star jar, shown while a golden sword is waiting for the castle.
        /// The component sits on an always-active object; only its "Icon" child is switched on and off.
        /// </summary>
        static void BuildGoldenSwordBadge(Transform canvas)
        {
            Sprite sword = ActivityBeaconBuilder.FindSprite(new[] { "Sword" });
            Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (sword == null) Debug.LogWarning("CoolSchool: sword sprite not found; the golden sword badge will be a plain gold square.");

            RectTransform root = NewRect("GoldenSwordBadge", canvas);
            SetAnchored(root, new Vector2(1f, 1f), new Vector2(-JarMargin - JarSize * 0.5f - BadgeSize * 0.5f - 20f, -JarMargin), new Vector2(BadgeSize, BadgeSize));

            RectTransform icon = NewStretched("Icon", root);
            AddImage(NewStretched("Backdrop", icon), round, new Color(0.2f, 0.12f, 0.35f, 0.6f));

            RectTransform swordRect = NewStretched("Sword", icon);
            swordRect.offsetMin = Vector2.one * (BadgeSize * 0.1f);
            swordRect.offsetMax = Vector2.one * (-BadgeSize * 0.1f);
            AddImage(swordRect, sword, new Color(1f, 0.82f, 0.15f, 1f));

            icon.gameObject.SetActive(false);

            var badge = root.gameObject.AddComponent<GoldenSwordBadge>();
            PlaygroundPrototypeBuilder.SetObject(badge, "icon", icon.gameObject);
        }

        /// <summary>The big sword of the "you won a golden sword" moment. It uses the same model as the knights' sword.</summary>
        static void BuildGoldenSwordReward()
        {
            GameObject prefab = PlaygroundPrototypeBuilder.LoadPrefab(PlaygroundPrototypeBuilder.Kids("SM_Wep_Sword_Foam_01"));
            if (prefab == null) Debug.LogWarning("CoolSchool: the foam sword model was not found, so the golden sword reward has nothing to show.");

            var rewardObject = new GameObject("GoldenSwordReward");
            var reward = rewardObject.AddComponent<GoldenSwordReward>();
            PlaygroundPrototypeBuilder.SetObject(reward, "swordPrefab", prefab);
        }

        /// <summary>
        /// The full-screen present popup, switched off until a present is won. It is last on the canvas,
        /// so it covers the joystick and the button while it is open.
        /// </summary>
        static PresentPopup BuildPresentPopup(Transform canvas, Sprite gift, Sprite star, RevealStage stage, RewardCatalog catalog)
        {
            RectTransform root = NewStretched("PresentPopup", canvas);
            Image dim = AddImage(root, null, new Color(0f, 0f, 0f, 0f));
            dim.raycastTarget = true;

            RectTransform rays = NewRect("Rays", root);
            SetAnchored(rays, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RaysSize, RaysSize));
            AddImage(rays, star, new Color(1f, 0.9f, 0.4f, 0.35f));

            RectTransform present = NewRect("Present", root);
            SetAnchored(present, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(PresentSize, PresentSize));
            AddImage(present, gift, Color.white);

            RectTransform reveal = NewRect("Reveal", root);
            SetAnchored(reveal, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(RevealSize, RevealSize));
            var revealImage = reveal.gameObject.AddComponent<RawImage>();
            revealImage.texture = GetOrCreateRenderTexture();
            revealImage.raycastTarget = false;

            // The medal: shown instead of the 3D item when everything has been won (Badge-Crown, or the trophy).
            Sprite medalSprite = ActivityBeaconBuilder.FindSprite(new[] { "Badge-Crown", "Trophy" });
            RectTransform medal = NewRect("Medal", root);
            SetAnchored(medal, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(MedalSize, MedalSize));
            Image medalImage = AddImage(medal, medalSprite, Color.white);
            medal.gameObject.SetActive(false);

            var popup = root.gameObject.AddComponent<PresentPopup>();
            PlaygroundPrototypeBuilder.SetObject(popup, "dim", dim);
            PlaygroundPrototypeBuilder.SetObject(popup, "present", present);
            PlaygroundPrototypeBuilder.SetObject(popup, "rays", rays);
            PlaygroundPrototypeBuilder.SetObject(popup, "revealImage", revealImage);
            PlaygroundPrototypeBuilder.SetObject(popup, "stage", stage);
            PlaygroundPrototypeBuilder.SetObject(popup, "catalog", catalog);
            PlaygroundPrototypeBuilder.SetObject(popup, "medalImage", medalImage);
            BuildSwapPanel(root, popup);

            root.SetAsLastSibling();
            root.gameObject.SetActive(false);
            return popup;
        }

        /// <summary>
        /// "Use the new one?": the picture of what he wears now on the left with an arrow to the spinning item,
        /// a big red cross (keep the old one) below left and a big green check (use the new one) below right.
        /// Switched off until the popup asks.
        /// </summary>
        static void BuildSwapPanel(RectTransform root, PresentPopup popup)
        {
            Sprite circle = ParentAreaBuilder.LoadShape("Circle");
            if (circle == null) circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite tick = ActivityBeaconBuilder.FindSprite(new[] { "Checkmark", "Check", "Tick" });
            Sprite cross = ActivityBeaconBuilder.FindSprite(new[] { "X-Delete-Close-Error", "X-Delete-Close-Error-Thin" });
            Sprite arrow = ActivityBeaconBuilder.FindSprite(new[] { "Arrow-Filled-Right", "Arrow-Right" });
            if (tick == null || cross == null) Debug.LogWarning("CoolSchool: the check or cross sprite was not found; the swap question will have plain buttons.");

            RectTransform panel = NewStretched("SwapPanel", root);

            // What he wears now, on a soft round card, with an arrow pointing at the new item.
            RectTransform card = NewRect("CurrentCard", panel);
            SetAnchored(card, new Vector2(0.5f, 0.5f), new Vector2(-CurrentCardX, 30f), Vector2.one * CurrentCardSize);
            AddImage(card, circle, new Color(1f, 1f, 1f, 0.25f));
            RectTransform currentRect = NewRect("CurrentIcon", card);
            SetAnchored(currentRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (CurrentCardSize * 0.8f));
            Image currentIcon = AddImage(currentRect, null, Color.white);

            RectTransform arrowRect = NewRect("Arrow", panel);
            SetAnchored(arrowRect, new Vector2(0.5f, 0.5f), new Vector2(-CurrentCardX + CurrentCardSize * 0.5f + ArrowSize * 0.7f, 30f), Vector2.one * ArrowSize);
            AddImage(arrowRect, arrow, new Color(1f, 0.9f, 0.4f, 1f));

            Button keepOld = BuildRoundButton("KeepOld", panel, circle, cross, new Color32(0xE8, 0x57, 0x4A, 0xFF), new Vector2(-SwapButtonX, SwapButtonY));
            Button useNew = BuildRoundButton("UseNew", panel, circle, tick, new Color32(0x4C, 0xC3, 0x5A, 0xFF), new Vector2(SwapButtonX, SwapButtonY));

            PlaygroundPrototypeBuilder.SetObject(popup, "swapPanel", panel);
            PlaygroundPrototypeBuilder.SetObject(popup, "currentIcon", currentIcon);
            PlaygroundPrototypeBuilder.SetObject(popup, "emptySlotSprite", cross);
            PlaygroundPrototypeBuilder.SetObject(popup, "useNewButton", useNew);
            PlaygroundPrototypeBuilder.SetObject(popup, "keepOldButton", keepOld);

            panel.gameObject.SetActive(false);
        }

        /// <summary>A big coloured circle with a white symbol on it that can be tapped.</summary>
        static Button BuildRoundButton(string name, Transform parent, Sprite circle, Sprite symbol, Color color, Vector2 position)
        {
            RectTransform disc = NewRect(name, parent);
            SetAnchored(disc, new Vector2(0.5f, 0.5f), position, Vector2.one * SwapButtonSize);
            Image image = AddImage(disc, circle, color);
            image.raycastTarget = true;

            RectTransform icon = NewRect("Icon", disc);
            SetAnchored(icon, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (SwapButtonSize * 0.55f));
            AddImage(icon, symbol, Color.white);

            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        // ----- Reward system -------------------------------------------------------------------

        static void BuildRewardSystem(RewardCatalog catalog, StarJarHud hud, PresentPopup popup)
        {
            var systemObject = new GameObject("RewardSystem");
            var system = systemObject.AddComponent<RewardSystem>();
            PlaygroundPrototypeBuilder.SetObject(system, "catalog", catalog);
            PlaygroundPrototypeBuilder.SetInt(system, "jarCapacity", 15);
            PlaygroundPrototypeBuilder.SetObject(system, "hud", hud);
            PlaygroundPrototypeBuilder.SetObject(system, "popup", popup);
            PlaygroundPrototypeBuilder.SetObject(system, "tagGame", Object.FindFirstObjectByType<TagGame>());
            PlaygroundPrototypeBuilder.SetObject(system, "knightsGame", Object.FindFirstObjectByType<KnightsGame>());
            PlaygroundPrototypeBuilder.SetObject(system, "alienLanding", Object.FindFirstObjectByType<AlienLanding>());
            PlaygroundPrototypeBuilder.SetObject(system, "pirateAdventure", Object.FindFirstObjectByType<CoolSchool.Pirates.PirateAdventure>());
            PlaygroundPrototypeBuilder.SetObject(system, "soapboxRace", Object.FindFirstObjectByType<CoolSchool.Race.SoapboxRace>());
            PlaygroundPrototypeBuilder.SetObject(system, "hideAndSeekGame", Object.FindFirstObjectByType<CoolSchool.Zoo.HideAndSeekGame>());
        }

        // ----- Helpers -------------------------------------------------------------------------

        /// <summary>A sprite with this exact name, preferring the Modular Game UI Kit and a path containing preferredPath.</summary>
        static Sprite FindSprite(string spriteName, string preferredPath)
        {
            Sprite best = null;
            int bestScore = -1;
            foreach (string guid in AssetDatabase.FindAssets(spriteName + " t:Sprite"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != spriteName) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                int score = (path.Contains("ModularGameUIKit") ? 1 : 0) + (path.Contains(preferredPath) ? 2 : 0);
                if (score > bestScore)
                {
                    best = sprite;
                    bestScore = score;
                }
            }

            if (best == null) Debug.LogWarning($"CoolSchool: sprite '{spriteName}' not found; the rewards picture will be a plain square.");
            return best;
        }

        /// <summary>Makes sure a user layer with this name exists. Returns its index, or -1 if there is no free slot.</summary>
        static int EnsureLayer(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("CoolSchool: could not open ProjectSettings/TagManager.asset.");
                return -1;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            int firstEmpty = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                string existing = layers.GetArrayElementAtIndex(i).stringValue;
                if (existing == layerName) return i;
                if (firstEmpty < 0 && string.IsNullOrEmpty(existing)) firstEmpty = i;
            }

            if (firstEmpty < 0)
            {
                Debug.LogWarning($"CoolSchool: no free user layer for '{layerName}'; using layer {FallbackLayer}.");
                return -1;
            }

            layers.GetArrayElementAtIndex(firstEmpty).stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return firstEmpty;
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static RectTransform NewStretched(string name, Transform parent)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Anchors to a single point (anchor == pivot == this point) with a fixed size.</summary>
        static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }
    }
}
