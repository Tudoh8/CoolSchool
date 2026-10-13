using CoolSchool.Parent;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Adds the parent area to the playground scene: the small gear (held for three seconds to open) on the touch UI,
    /// and the parent panel on a canvas of its own above everything. Called once by the playground builder after the
    /// touch UI exists and before the rewards (so the present popup still covers the gear).
    /// </summary>
    public static class ParentAreaBuilder
    {
        const string TouchCanvasName = "TouchUI";
        const string PanelName = "ParentPanel";
        const string FontName = "Inter-Bold";
        const string ShapesFolder = "Assets/Imported/ModularGameUIKit/Common/Sprites/Shapes/";

        // The gear: small and dim, in the top-left corner, away from the joystick, the action button and the star jar.
        const float GearHitSize = 150f;
        const float GearInset = 110f;
        const float GearBackdropSize = 100f;
        const float GearIconSize = 64f;
        const float GearRingSize = 124f;

        // The panel (canvas units on a 1920x1080 reference, matched to the height like the touch UI).
        const float WindowWidth = 1300f;
        const float WindowHeight = 1068f;
        const float Margin = 70f;
        const float ContentWidth = WindowWidth - 2f * Margin;
        const int PanelSortingOrder = 1000;

        const float SliderX = 360f;
        const float SliderWidth = 640f;
        const float SliderHeight = 76f;
        const float HandleSize = 76f;
        const float TrackHeight = 30f;

        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        static readonly Color WindowColor = new Color(0.17f, 0.15f, 0.32f, 1f);
        static readonly Color TextColor = new Color(0.97f, 0.95f, 0.88f, 1f);
        static readonly Color MutedColor = new Color(0.74f, 0.72f, 0.86f, 1f);
        static readonly Color AccentColor = new Color(1f, 0.8f, 0.25f, 1f);
        static readonly Color TrackColor = new Color(0.09f, 0.08f, 0.2f, 1f);
        static readonly Color FieldColor = new Color(0.97f, 0.95f, 0.9f, 1f);
        static readonly Color FieldTextColor = new Color(0.15f, 0.13f, 0.25f, 1f);
        static readonly Color CloseColor = new Color(0.3f, 0.74f, 0.42f, 1f);

        sealed class Sprites
        {
            public Sprite Panel;
            public Sprite Circle;
            public Sprite CircleOutline;
            public Sprite Gear;
        }

        public static void Build()
        {
            GameObject touchCanvas = GameObject.Find(TouchCanvasName);
            if (touchCanvas == null)
            {
                Debug.LogWarning($"CoolSchool: no '{TouchCanvasName}' canvas found, so the parent area was not built.");
                return;
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                Debug.LogWarning("CoolSchool: no EventSystem in the scene; the parent area will not react to touches.");

            // Re-running replaces what was built before.
            GameObject old = GameObject.Find(PanelName);
            if (old != null) Object.DestroyImmediate(old);

            var sprites = new Sprites
            {
                Panel = LoadShape("Background"),
                Circle = LoadShape("Circle"),
                CircleOutline = LoadShape("Circle-Outline"),
                Gear = ActivityBeaconBuilder.FindSprite(new[] { "Settings-Wheel", "Settings-Slider" }),
            };
            if (sprites.Gear == null) Debug.LogWarning("CoolSchool: the gear sprite (Settings-Wheel) was not found; the parent gear will be a plain dot.");

            Font font = FindFont();
            ParentPanel panel = BuildPanel(sprites, font);
            BuildGear(touchCanvas.transform, sprites, panel);
        }

        // ----- The gear --------------------------------------------------------------------------

        static void BuildGear(Transform canvas, Sprites sprites, ParentPanel panel)
        {
            // The touch area is a bit larger than the picture. Transparent, but it takes the touches in its corner
            // (it stands on top of the joystick zone, so only this small square is taken from it).
            RectTransform root = NewRect("ParentGear", canvas);
            SetAnchored(root, new Vector2(0f, 1f), new Vector2(GearInset, -GearInset), new Vector2(GearHitSize, GearHitSize));
            AddImage(root, null, new Color(0f, 0f, 0f, 0f), true);

            RectTransform visual = NewRect("Visual", root);
            SetAnchored(visual, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GearHitSize, GearHitSize));

            RectTransform backdrop = NewRect("Backdrop", visual);
            SetAnchored(backdrop, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * GearBackdropSize);
            AddImage(backdrop, sprites.Circle, new Color(0f, 0f, 0f, 0.22f), false);

            RectTransform icon = NewRect("Icon", visual);
            SetAnchored(icon, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * GearIconSize);
            AddImage(icon, sprites.Gear, new Color(1f, 1f, 1f, 0.4f), false);

            RectTransform ringRect = NewRect("HoldRing", visual);
            SetAnchored(ringRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * GearRingSize);
            Image ring = AddImage(ringRect, sprites.CircleOutline != null ? sprites.CircleOutline : sprites.Circle, AccentColor, false);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;

            var button = root.gameObject.AddComponent<ParentAreaButton>();
            PlaygroundPrototypeBuilder.SetObject(button, "panel", panel);
            PlaygroundPrototypeBuilder.SetObject(button, "ring", ring);
            PlaygroundPrototypeBuilder.SetObject(button, "visual", visual);

            // Below the present popup (if it is already there), so the popup still covers the gear.
            Transform popup = canvas.Find("PresentPopup");
            if (popup != null) root.SetSiblingIndex(popup.GetSiblingIndex());
        }

        // ----- The panel -------------------------------------------------------------------------

        static ParentPanel BuildPanel(Sprites sprites, Font font)
        {
            var canvasObject = new GameObject(PanelName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PanelSortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var panel = canvasObject.AddComponent<ParentPanel>();
            var root = (RectTransform)canvasObject.transform;

            // Everything shown sits under "Content", which is switched off while the panel is closed.
            RectTransform content = NewStretched("Content", root);
            var group = content.gameObject.AddComponent<CanvasGroup>();

            // The dim backdrop takes every touch, so nothing behind the panel (joystick, button, lessons) can be reached.
            AddImage(NewStretched("Dim", content), null, DimColor, true);

            RectTransform window = NewRect("Window", content);
            SetAnchored(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, WindowHeight));
            Image windowImage = AddImage(window, sprites.Panel, WindowColor, true);
            windowImage.type = Image.Type.Sliced;
            windowImage.preserveAspect = false;

            Text title = AddText(window, "Title", font, 68, AccentColor, TextAnchor.MiddleCenter, 0f, 30f, WindowWidth, 90f);

            // Name
            Text nameLabel = AddText(window, "NameLabel", font, 40, TextColor, TextAnchor.MiddleLeft, Margin, 112f, ContentWidth, 50f);
            InputField nameField = BuildNameField(window, sprites, font, Margin, 162f, 560f, 96f, out Text placeholder);
            Text nameHint = AddText(window, "NameHint", font, 30, MutedColor, TextAnchor.MiddleLeft, Margin + 590f, 162f, ContentWidth - 590f, 96f);

            // Difficulty
            Text difficultyLabel = AddText(window, "DifficultyLabel", font, 40, TextColor, TextAnchor.MiddleLeft, Margin, 268f, ContentWidth, 50f);

            // Eight rows, 78 px apart from y = 318 (the sliders are 76 tall), so the last one ends above the close button (top y = 948).
            // The one caption sits on the difficulty line next to its label, so it needs no room under the last row.
            var rows = new[]
            {
                BuildLevelRow(window, sprites, font, 318f, ParentStrings.RowNumbers, "numbers", CoolSchool.Lessons.NumberCurriculum.MaxLevel),
                BuildLevelRow(window, sprites, font, 396f, ParentStrings.RowLetters, "letters", 5),
                BuildLevelRow(window, sprites, font, 474f, ParentStrings.RowShop, ParentPanel.ShopSkill, 3),
                BuildLevelRow(window, sprites, font, 552f, ParentStrings.RowPirates, "pirates", 3),
                BuildLevelRow(window, sprites, font, 630f, ParentStrings.RowRace, "race", 3),
                BuildLevelRow(window, sprites, font, 708f, ParentStrings.RowAliens, "aliens", 3),
                BuildLevelRow(window, sprites, font, 786f, ParentStrings.RowCastle, "castle", 3),
                BuildLevelRow(window, sprites, font, 864f, ParentStrings.RowHideSeek, "hideseek", 3),
            };
            rows[0].Caption = AddText(window, "LevelCaption", font, 24, MutedColor, TextAnchor.MiddleLeft,
                SliderX + 40f, 268f, WindowWidth - Margin - SliderX - 40f, 50f);

            // Close
            const float closeWidth = 460f;
            const float closeHeight = 96f;
            RectTransform closeRect = NewRect("CloseButton", window);
            Place(closeRect, (WindowWidth - closeWidth) * 0.5f, WindowHeight - closeHeight - 24f, closeWidth, closeHeight);
            Image closeImage = AddImage(closeRect, sprites.Panel, CloseColor, true);
            closeImage.type = Image.Type.Sliced;
            closeImage.preserveAspect = false;
            var closeButton = closeRect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            Text closeLabel = AddText(closeRect, "Label", font, 58, TextColor, TextAnchor.MiddleCenter, 0f, 0f, closeWidth, closeHeight);

            // Wire it all up.
            PlaygroundPrototypeBuilder.SetObject(panel, "content", content.gameObject);
            PlaygroundPrototypeBuilder.SetObject(panel, "contentGroup", group);
            PlaygroundPrototypeBuilder.SetObject(panel, "window", window);
            PlaygroundPrototypeBuilder.SetObject(panel, "titleText", title);
            PlaygroundPrototypeBuilder.SetObject(panel, "nameLabel", nameLabel);
            PlaygroundPrototypeBuilder.SetObject(panel, "nameHint", nameHint);
            PlaygroundPrototypeBuilder.SetObject(panel, "difficultyLabel", difficultyLabel);
            PlaygroundPrototypeBuilder.SetObject(panel, "closeLabel", closeLabel);
            PlaygroundPrototypeBuilder.SetObject(panel, "nameField", nameField);
            PlaygroundPrototypeBuilder.SetObject(panel, "namePlaceholder", placeholder);
            PlaygroundPrototypeBuilder.SetObject(panel, "closeButton", closeButton);
            SetRows(panel, rows);

            content.gameObject.SetActive(false);
            return panel;
        }

        sealed class RowParts
        {
            public string Skill;
            public string LabelKey;
            public int MaxLevel;
            public Text Label;
            public Text Caption;
            public Text Value;
            public Slider Slider;
        }

        static RowParts BuildLevelRow(RectTransform window, Sprites sprites, Font font, float y, string labelKey, string skill, int maxLevel,
            bool withCaption = false)
        {
            var parts = new RowParts { Skill = skill, LabelKey = labelKey, MaxLevel = maxLevel };

            parts.Label = AddText(window, skill + "Label", font, 44, TextColor, TextAnchor.MiddleLeft, Margin, y, SliderX - Margin - 20f, SliderHeight);
            // A long label ("Verstoppertje", "Hide and seek") shrinks to stay on one line instead of wrapping into the next row.
            parts.Label.resizeTextForBestFit = true;
            parts.Label.resizeTextMinSize = 28;
            parts.Label.resizeTextMaxSize = 44;
            parts.Slider = BuildSlider(window, sprites, skill + "Slider", SliderX, y, maxLevel);
            parts.Value = AddText(window, skill + "Value", font, 76, AccentColor, TextAnchor.MiddleCenter,
                SliderX + SliderWidth + 20f, y, WindowWidth - Margin - (SliderX + SliderWidth + 20f), SliderHeight);
            // The rows are too close together for a caption each: one caption under the last row serves them all.
            if (withCaption)
                parts.Caption = AddText(window, skill + "Caption", font, 28, MutedColor, TextAnchor.MiddleLeft,
                    SliderX, y + SliderHeight - 2f, WindowWidth - Margin - SliderX, 34f);
            return parts;
        }

        /// <summary>A whole-number slider with a thick track and a big round handle that is easy to grab.</summary>
        static Slider BuildSlider(RectTransform parent, Sprites sprites, string name, float x, float y, int maxLevel)
        {
            RectTransform root = NewRect(name, parent);
            Place(root, x, y, SliderWidth, SliderHeight);
            // A taller invisible area, so a touch near the track still counts.
            AddImage(root, null, new Color(0f, 0f, 0f, 0f), true);

            RectTransform track = NewRect("Background", root);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(0f, TrackHeight);
            Image trackImage = AddImage(track, sprites.Panel, TrackColor, false);
            trackImage.type = Image.Type.Sliced;
            trackImage.preserveAspect = false;

            // Fill and handle share the same inset, so the fill ends exactly under the middle of the handle.
            RectTransform fillArea = NewRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(HandleSize * 0.5f, -TrackHeight * 0.5f);
            fillArea.offsetMax = new Vector2(-HandleSize * 0.5f, TrackHeight * 0.5f);

            RectTransform fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = AddImage(fill, sprites.Panel, AccentColor, false);
            fillImage.type = Image.Type.Sliced;
            fillImage.preserveAspect = false;

            RectTransform slideArea = NewRect("Handle Slide Area", root);
            slideArea.anchorMin = Vector2.zero;
            slideArea.anchorMax = Vector2.one;
            slideArea.offsetMin = new Vector2(HandleSize * 0.5f, 0f);
            slideArea.offsetMax = new Vector2(-HandleSize * 0.5f, 0f);

            RectTransform handle = NewRect("Handle", slideArea);
            handle.anchorMin = new Vector2(0.5f, 0f);
            handle.anchorMax = new Vector2(0.5f, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(HandleSize, 0f);
            Image handleImage = AddImage(handle, sprites.Circle, Color.white, true);
            handleImage.preserveAspect = true;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 1f;
            slider.maxValue = maxLevel;
            slider.wholeNumbers = true;
            slider.value = 1f;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        static InputField BuildNameField(RectTransform parent, Sprites sprites, Font font, float x, float y, float width, float height, out Text placeholder)
        {
            RectTransform root = NewRect("NameField", parent);
            Place(root, x, y, width, height);
            Image background = AddImage(root, sprites.Panel, FieldColor, true);
            background.type = Image.Type.Sliced;
            background.preserveAspect = false;

            RectTransform textRect = NewStretched("Text", root);
            textRect.offsetMin = new Vector2(28f, 8f);
            textRect.offsetMax = new Vector2(-28f, -8f);
            Text text = AddTextComponent(textRect, font, 54, FieldTextColor, TextAnchor.MiddleLeft);
            text.supportRichText = false;

            RectTransform placeholderRect = NewStretched("Placeholder", root);
            placeholderRect.offsetMin = textRect.offsetMin;
            placeholderRect.offsetMax = textRect.offsetMax;
            placeholder = AddTextComponent(placeholderRect, font, 54, new Color(FieldTextColor.r, FieldTextColor.g, FieldTextColor.b, 0.35f), TextAnchor.MiddleLeft);
            placeholder.fontStyle = FontStyle.Italic;

            var input = root.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 12;
            input.caretColor = FieldTextColor;
            input.customCaretColor = true;
            input.selectionColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.45f);
            input.transition = Selectable.Transition.None;
            return input;
        }

        /// <summary>Writes the rows into the panel's serialized "rows" array.</summary>
        static void SetRows(ParentPanel panel, RowParts[] rows)
        {
            var serialized = new SerializedObject(panel);
            SerializedProperty array = serialized.FindProperty("rows");
            array.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("skill").stringValue = rows[i].Skill;
                element.FindPropertyRelative("labelKey").stringValue = rows[i].LabelKey;
                element.FindPropertyRelative("maxLevel").intValue = rows[i].MaxLevel;
                element.FindPropertyRelative("label").objectReferenceValue = rows[i].Label;
                element.FindPropertyRelative("caption").objectReferenceValue = rows[i].Caption;
                element.FindPropertyRelative("valueText").objectReferenceValue = rows[i].Value;
                element.FindPropertyRelative("slider").objectReferenceValue = rows[i].Slider;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----- Helpers ---------------------------------------------------------------------------

        internal static Sprite LoadShape(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShapesFolder + name + ".png");
            if (sprite != null) return sprite;

            sprite = ActivityBeaconBuilder.FindSprite(new[] { name });
            if (sprite == null) Debug.LogWarning($"CoolSchool: UI sprite '{name}' not found; the parent area will use plain squares.");
            return sprite;
        }

        static Font FindFont()
        {
            foreach (string guid in AssetDatabase.FindAssets(FontName + " t:Font"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != FontName) continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null) return font;
            }

            Debug.LogWarning($"CoolSchool: font '{FontName}' not found; the parent area will use the default font.");
            return null;
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        internal static RectTransform NewStretched(string name, Transform parent)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Anchors to a single point (anchor == pivot == this point) with a fixed size.</summary>
        internal static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Places a rectangle by its top-left corner, measured from the top-left of its parent, y growing downwards.</summary>
        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static Image AddImage(RectTransform rect, Sprite sprite, Color color, bool raycastTarget)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            image.preserveAspect = true;
            return image;
        }

        static Text AddText(RectTransform parent, string name, Font font, int size, Color color, TextAnchor anchor,
            float x, float y, float width, float height)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, x, y, width, height);
            return AddTextComponent(rect, font, size, color, anchor);
        }

        static Text AddTextComponent(RectTransform rect, Font font, int size, Color color, TextAnchor anchor)
        {
            var text = rect.gameObject.AddComponent<Text>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
