using System.Collections.Generic;
using CoolSchool.Race;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The race HUD on the touch canvas: the countdown digit, the coin counter, the place badge, the progress strip, the target
    /// bubble and the two how-to arrows. Digits and pictures only. Everything ignores touches.
    /// </summary>
    internal static partial class SoapboxStreetBuilder
    {
        const string CanvasName = "TouchUI";
        const string FontName = "Inter-Bold";

        // The gear sits in the top-left corner (to x 185 at the 1920 x 1080 reference); the star jar in the top-right.
        static readonly Color PanelColour = new Color(0.08f, 0.1f, 0.2f, 0.5f);
        static readonly Color DarkText = new Color(0.12f, 0.12f, 0.2f, 1f);

        static Font FindFont()
        {
            foreach (string guid in AssetDatabase.FindAssets(FontName + " t:Font"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != FontName) continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null) return font;
            }
            Debug.LogWarning($"CoolSchool race street: font '{FontName}' not found; the race digits use the default font.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static RaceHud BuildHud(Font font)
        {
            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas == null)
            {
                Debug.LogWarning($"CoolSchool race street: no '{CanvasName}' canvas found, so the race HUD was not built.");
                return null;
            }

            Sprite circle = ShopAssets.FindShape("Circle");
            Sprite panel = ShopAssets.FindShape("Background");
            Sprite outline = ShopAssets.FindShape("Background-Outline");
            Sprite coinSprite = ActivityBeaconBuilder.FindSprite(new[] { "Coin", "Coins" });
            Sprite leftArrow = ActivityBeaconBuilder.FindSprite(new[] { "Arrow-Filled-Left", "Arrow-Single-Left", "Arrow-Left", "Arrow" });
            Sprite rightArrow = ActivityBeaconBuilder.FindSprite(new[] { "Arrow-Filled-Right", "Arrow-Single-Right", "Arrow-Right", "Arrow" });
            if (coinSprite == null) Debug.LogWarning("CoolSchool race street: no 'Coin' sprite found; the coin counter shows a gold circle.");
            if (leftArrow == null || rightArrow == null) Debug.LogWarning("CoolSchool race street: no arrow sprites found; the how-to arrows are plain squares.");

            // The component sits on a holder that stays active; everything it shows is under "Root", which Show() switches.
            RectTransform holder = UiRect("RaceHud", canvas.transform);
            UiStretch(holder);
            var hud = holder.gameObject.AddComponent<RaceHud>();
            RectTransform root = UiRect("Root", holder);
            UiStretch(root);

            // Countdown: one huge digit in the middle.
            Text countdown = UiText("Countdown", root, font, 420, Color.white, TextAnchor.MiddleCenter);
            UiAnchored(countdown.rectTransform, new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(900f, 600f));
            countdown.text = string.Empty;
            countdown.gameObject.AddComponent<Outline>().effectDistance = new Vector2(8f, -8f);

            // Coin counter, right of the parent gear.
            RectTransform counter = UiRect("CoinCounter", root);
            UiAnchored(counter, new Vector2(0f, 1f), new Vector2(370f, -110f), new Vector2(250f, 92f));
            UiImage(counter, panel, PanelColour);
            RectTransform coinIcon = UiRect("CoinIcon", counter);
            UiAnchored(coinIcon, new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(72f, 72f));
            UiImage(coinIcon, coinSprite != null ? coinSprite : circle, coinSprite != null ? Color.white : new Color(1f, 0.8f, 0.15f));
            Text coinText = UiText("CoinText", counter, font, 62, Color.white, TextAnchor.MiddleLeft);
            coinText.rectTransform.anchorMin = Vector2.zero;
            coinText.rectTransform.anchorMax = Vector2.one;
            coinText.rectTransform.offsetMin = new Vector2(100f, 0f);
            coinText.rectTransform.offsetMax = new Vector2(-10f, 0f);
            coinText.text = "0";

            // Place badge: a gold circle with the place digit.
            RectTransform badgeRect = UiRect("PlaceBadge", root);
            UiAnchored(badgeRect, new Vector2(0f, 1f), new Vector2(585f, -110f), new Vector2(100f, 100f));
            Image badge = UiImage(badgeRect, circle, new Color(1f, 0.8f, 0.2f, 1f));
            Text placeText = UiText("PlaceText", badgeRect, font, 66, DarkText, TextAnchor.MiddleCenter);
            UiStretch(placeText.rectTransform);
            placeText.text = "1";

            // Progress strip with three dots (the player first, bigger).
            RectTransform bar = UiRect("ProgressBar", root);
            UiAnchored(bar, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(420f, 24f));
            UiImage(bar, panel, new Color(1f, 1f, 1f, 0.55f));
            Color[] dotColours = { new Color(1f, 0.8f, 0.2f), new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.55f, 0.95f) };
            float[] dotSizes = { 46f, 34f, 34f };
            var dots = new List<RectTransform>();
            for (int i = 0; i < 3; i++)
            {
                RectTransform dot = UiRect("Dot_" + (i + 1), bar);
                UiAnchored(dot, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(dotSizes[i], dotSizes[i]));
                UiImage(dot, circle, dotColours[i]);
                dots.Add(dot);
            }

            // Target bubble: the digit or the letter to drive through, big.
            RectTransform bubble = UiRect("TargetBubble", root);
            UiAnchored(bubble, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(250f, 230f));
            UiImage(bubble, panel, new Color(1f, 1f, 1f, 0.95f));
            RectTransform bubbleEdge = UiRect("Edge", bubble);
            UiStretch(bubbleEdge);
            UiImage(bubbleEdge, outline, new Color(0.3f, 0.45f, 0.9f, 1f));
            Text targetText = UiText("TargetText", bubble, font, 190, DarkText, TextAnchor.MiddleCenter);
            UiStretch(targetText.rectTransform);
            targetText.text = string.Empty;

            // How-to arrows, one on each half of the screen.
            RectTransform left = UiRect("LeftArrow", root);
            UiAnchored(left, new Vector2(0.18f, 0.45f), Vector2.zero, new Vector2(220f, 220f));
            Image leftImage = UiImage(left, leftArrow, new Color(1f, 1f, 1f, 0.85f));
            RectTransform right = UiRect("RightArrow", root);
            UiAnchored(right, new Vector2(0.82f, 0.45f), Vector2.zero, new Vector2(220f, 220f));
            Image rightImage = UiImage(right, rightArrow, new Color(1f, 1f, 1f, 0.85f));

            // Honk button: a round button above the jump button (which is 260 wide at -230 / 230), with the picture of the horn he chose.
            // It is the only part of the HUD that takes touches; the race input ignores taps that start on it.
            RectTransform honk = UiRect("HonkButton", root);
            UiAnchored(honk, new Vector2(1f, 0f), new Vector2(-170f, 505f), new Vector2(HonkButtonSize, HonkButtonSize));
            Image honkBack = UiImage(honk, circle, new Color(1f, 0.84f, 0.25f, 0.95f));
            honkBack.raycastTarget = true;
            var honkButton = honk.gameObject.AddComponent<Button>();
            honkButton.targetGraphic = honkBack;
            honkButton.transition = Selectable.Transition.None;
            honkButton.navigation = new Navigation { mode = Navigation.Mode.None };
            RectTransform honkIcon = UiRect("Icon", honk);
            UiAnchored(honkIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HonkButtonSize * 0.74f, HonkButtonSize * 0.74f));
            Image honkIconImage = UiImage(honkIcon, null, Color.white);
            honkIconImage.enabled = false;

            SetObject(hud, "honkButton", honkButton);
            SetObject(hud, "honkIcon", honkIconImage);
            SetObject(hud, "root", root.gameObject);
            SetObject(hud, "countdownText", countdown);
            SetObject(hud, "coinCounter", counter);
            SetObject(hud, "coinText", coinText);
            SetObject(hud, "placeBadge", badge);
            SetObject(hud, "placeText", placeText);
            SetObject(hud, "progressBar", bar);
            SetObjectArray(hud, "progressDots", dots);
            SetObject(hud, "targetBubble", bubble);
            SetObject(hud, "targetText", targetText);
            SetObject(hud, "leftArrow", leftImage);
            SetObject(hud, "rightArrow", rightImage);

            root.gameObject.SetActive(false);
            return hud;
        }

        // ---- The cart picker ----

        const string RewardCatalogPath = "Assets/CoolSchool/Rewards/Catalog.asset";
        const float GoButtonSize = 210f;
        const float HonkButtonSize = 170f;
        // The panel along the bottom (1920 x 1080 reference): six tabs in a column on the left, two rows of six cards.
        const float PickerStripHeight = 360f;
        const float PickerTabSize = 100f;
        const float PickerTabGap = 8f;
        const float PickerCardAreaLeft = 190f;
        static readonly Color TabBackColour = new Color(0.85f, 0.88f, 1f, 0.92f);
        static readonly Color TabPictureColour = new Color(0.18f, 0.14f, 0.36f, 1f);
        static readonly Color GoGreen = new Color(0.3f, 0.76f, 0.35f, 1f);

        // The pictures on the tabs: the icon of one item each (the number tab shows the digits "123" instead).
        static readonly string[] PickerTabIconItems =
        {
            "cart.racer_1", CartGarageData.PaintRed, CartGarageData.Balloons, CartGarageData.TrailRainbow, CartGarageData.HornBell, "",
        };

        /// <summary>
        /// The cart picker on the touch canvas (the cart garage): a strip along the bottom with six round tabs in a column on the left
        /// (body, paint, add-ons, trail, horn, number), the area where CartPicker makes the picture cards at run time (up to 12, two
        /// rows of six), and the green check at the bottom right. Pictures and digits only. Each tab is a Button with a child "Icon"
        /// (CartPicker puts the item's picture on it) and a child "New" (a star on a tab with things he has not seen). Hidden until the
        /// race opens it.
        /// </summary>
        static CartPicker BuildCartPicker(Font font)
        {
            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas == null)
            {
                Debug.LogWarning($"CoolSchool race street: no '{CanvasName}' canvas found, so the cart picker was not built.");
                return null;
            }

            // The catalog holds the pictures and the cards. It is a saved asset, so the rewards setup (later in the build) fills in the icons.
            var catalog = AssetDatabase.LoadAssetAtPath<CoolSchool.Rewards.RewardCatalog>(RewardCatalogPath);
            if (catalog == null) catalog = RewardCatalogSeeder.CreateOrUpdate();

            Sprite circle = ShopAssets.FindShape("Circle");
            Sprite panel = ShopAssets.FindShape("Background");
            Sprite outline = ShopAssets.FindShape("Background-Outline");
            Sprite check = ActivityBeaconBuilder.FindSprite(new[] { "Checkmark" });
            Sprite lockSprite = ActivityBeaconBuilder.FindSprite(new[] { "Lock" });
            Sprite none = ActivityBeaconBuilder.FindSprite(new[] { "X-Delete-Close-Error" });
            Sprite star = ActivityBeaconBuilder.FindSprite(new[] { "Star" });
            if (check == null) Debug.LogWarning("CoolSchool race street: no 'Checkmark' sprite found; the go button is a plain green circle.");
            if (lockSprite == null) Debug.LogWarning("CoolSchool race street: no 'Lock' sprite found; locked cards show only a dark dot.");

            RectTransform holder = UiRect("CartPicker", canvas.transform);
            UiStretch(holder);
            var picker = holder.gameObject.AddComponent<CartPicker>();
            RectTransform root = UiRect("Root", holder);
            UiStretch(root);

            // A soft strip behind the cards, so they read against the street.
            RectTransform strip = UiRect("Strip", root);
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(1f, 0f);
            strip.pivot = new Vector2(0.5f, 0f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = new Vector2(0f, PickerStripHeight);
            UiImage(strip, panel, new Color(0.08f, 0.1f, 0.2f, 0.45f));

            // The six tabs, body at the top.
            var tabs = new List<Button>();
            for (int i = 0; i < 6; i++)
            {
                float y = 28f + PickerTabSize * 0.5f + (5 - i) * (PickerTabSize + PickerTabGap);
                tabs.Add(BuildPickerTab(root, i, new Vector2(PickerCardAreaLeft * 0.5f, y), circle, star, font));
            }

            // The cards are made here at run time: the first card's top left corner is the area's top left.
            RectTransform cardArea = UiRect("Cards", root);
            cardArea.anchorMin = cardArea.anchorMax = new Vector2(0f, 0f);
            cardArea.pivot = new Vector2(0f, 1f);
            cardArea.anchoredPosition = new Vector2(PickerCardAreaLeft, PickerStripHeight - 24f);
            cardArea.sizeDelta = new Vector2(960f, 312f);

            // The go button.
            RectTransform go = UiRect("GoButton", root);
            UiAnchored(go, new Vector2(1f, 0f), new Vector2(-150f, 160f), new Vector2(GoButtonSize, GoButtonSize));
            Image goImage = UiImage(go, circle, GoGreen);
            goImage.raycastTarget = true;
            var goButton = go.gameObject.AddComponent<Button>();
            goButton.targetGraphic = goImage;
            goButton.transition = Selectable.Transition.None;
            goButton.navigation = new Navigation { mode = Navigation.Mode.None };
            RectTransform goIcon = UiRect("Icon", go);
            UiAnchored(goIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GoButtonSize * 0.6f, GoButtonSize * 0.6f));
            UiImage(goIcon, check != null ? check : circle, check != null ? Color.white : new Color(1f, 1f, 1f, 0.9f));

            SetObject(picker, "root", root.gameObject);
            SetObject(picker, "catalog", catalog);
            SetObjectArray(picker, "tabButtons", tabs);
            SetStringArray(picker, "tabIconItems", PickerTabIconItems);
            SetObject(picker, "cardArea", cardArea);
            SetObject(picker, "goButton", goButton);
            SetObject(picker, "cardSprite", panel);
            SetObject(picker, "ringSprite", outline);
            SetObject(picker, "circleSprite", circle);
            SetObject(picker, "checkSprite", check);
            SetObject(picker, "lockSprite", lockSprite);
            SetObject(picker, "noneSprite", none);
            SetObject(picker, "newSprite", star);
            SetObject(picker, "font", font);

            root.gameObject.SetActive(false);
            return picker;
        }

        /// <summary>One round tab: a light circle (the button's own graphic, tinted by the picker), its picture ("Icon", or the digits "123" on the number tab) and a hidden "New" star.</summary>
        static Button BuildPickerTab(Transform parent, int index, Vector2 position, Sprite circle, Sprite star, Font font)
        {
            RectTransform tab = UiRect("Tab_" + index, parent);
            UiAnchored(tab, new Vector2(0f, 0f), position, new Vector2(PickerTabSize, PickerTabSize));
            Image back = UiImage(tab, circle, TabBackColour);
            back.raycastTarget = true;
            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            if (index == 5)
            {
                // The number tab: "123" made of digits.
                Text digits = UiText("Digits", tab, font, 44, TabPictureColour, TextAnchor.MiddleCenter);
                digits.fontStyle = FontStyle.Bold;
                UiStretch(digits.rectTransform);
                digits.text = "123";
            }
            else
            {
                RectTransform icon = UiRect("Icon", tab);
                UiAnchored(icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PickerTabSize * 0.8f, PickerTabSize * 0.8f));
                UiImage(icon, null, Color.white);
            }

            RectTransform news = UiRect("New", tab);
            UiAnchored(news, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(PickerTabSize * 0.38f, PickerTabSize * 0.38f));
            UiImage(news, star != null ? star : circle, new Color(1f, 0.85f, 0.2f, 1f));
            news.gameObject.SetActive(false);
            return button;
        }


        // ---- Small UI helpers (the shared ones are private to the playground builder) ----

        static RectTransform UiRect(string rectName, Transform parent)
        {
            var go = new GameObject(rectName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static void UiStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Anchors to a single point (anchor == pivot == the point) with a fixed size.</summary>
        static void UiAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Image UiImage(RectTransform rect, Sprite sprite, Color colour)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        static Text UiText(string textName, Transform parent, Font font, int size, Color colour, TextAnchor alignment)
        {
            RectTransform rect = UiRect(textName, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        internal static void SetString(Object target, string field, string value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"CoolSchool: {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
