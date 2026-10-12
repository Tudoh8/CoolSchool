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
