using CoolSchool.Activities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Adds the quit button to the playground scene: a round back arrow in the top-left corner of the touch UI (right of the
    /// parent gear) and the "do you want to stop?" prompt on a canvas of its own, with a green check and a red cross. No text,
    /// because the child cannot read. Called once by the playground builder right after the parent area.
    /// </summary>
    public static class QuitButtonBuilder
    {
        const string TouchCanvasName = "TouchUI";
        const string ButtonName = "QuitButton";
        const string PromptName = "QuitPrompt";

        // The button: right of the parent gear (gear centre at inset 110, hit size 150).
        static readonly Vector2 ButtonPosition = new Vector2(290f, -110f);
        const float ButtonHitSize = 160f;
        const float ButtonBackdropSize = 146f;
        const float ButtonCircleSize = 130f;
        const float ButtonIconSize = 80f;

        // The prompt (canvas units on a 1920x1080 reference, matched to the height like the touch UI).
        const int PromptSortingOrder = 900;
        const float WindowWidth = 1000f;
        const float WindowHeight = 720f;
        const float ArrowSize = 210f;
        const float ArrowY = 185f;
        const float ChoiceSize = 260f;
        const float ChoiceX = 230f;
        const float ChoiceY = -140f;
        const float ChoiceIconSize = 160f;

        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color WindowColor = new Color(0.17f, 0.15f, 0.32f, 1f);
        static readonly Color ButtonColor = new Color(1f, 0.6f, 0.2f, 1f);
        static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.3f);
        static readonly Color StopColor = new Color(0.3f, 0.74f, 0.42f, 1f);
        static readonly Color ContinueColor = new Color(0.9f, 0.28f, 0.28f, 1f);

        public static void Build()
        {
            GameObject touchCanvas = GameObject.Find(TouchCanvasName);
            if (touchCanvas == null)
            {
                Debug.LogWarning($"CoolSchool: no '{TouchCanvasName}' canvas found, so the quit button was not built.");
                return;
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                Debug.LogWarning("CoolSchool: no EventSystem in the scene; the quit button will not react to touches.");

            // Re-running replaces what was built before.
            DestroyByName(ButtonName);
            DestroyByName(PromptName);

            Sprite circle = ParentAreaBuilder.LoadShape("Circle");
            Sprite window = ParentAreaBuilder.LoadShape("Background");
            Sprite arrow = ActivityBeaconBuilder.FindSprite(new[] { "Arrow-Left" });
            Sprite tick = ActivityBeaconBuilder.FindSprite(new[] { "Checkmark", "Check", "Tick" });
            Sprite cross = ActivityBeaconBuilder.FindSprite(new[] { "X-Delete-Close-Error", "X-Delete-Close-Error-Thin" });
            if (arrow == null) Debug.LogWarning("CoolSchool: the back arrow sprite (Arrow-Left) was not found; the quit button will have no arrow.");
            if (tick == null || cross == null) Debug.LogWarning("CoolSchool: the check or cross sprite was not found; the quit prompt will have plain buttons.");

            QuitPrompt prompt = BuildPrompt(circle, window, arrow, tick, cross);
            BuildButton(touchCanvas.transform, circle, arrow, prompt);
        }

        static void DestroyByName(string name)
        {
            GameObject old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        // ----- The button ------------------------------------------------------------------------

        static void BuildButton(Transform canvas, Sprite circle, Sprite arrow, QuitPrompt prompt)
        {
            // The root only positions; the Visual child holds the touch area and the picture, so while the button is hidden
            // nothing in the corner takes touches away from the joystick.
            RectTransform root = ParentAreaBuilder.NewRect(ButtonName, canvas);
            ParentAreaBuilder.SetAnchored(root, new Vector2(0f, 1f), ButtonPosition, Vector2.one * ButtonHitSize);

            RectTransform visual = ParentAreaBuilder.NewRect("Visual", root);
            ParentAreaBuilder.SetAnchored(visual, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * ButtonHitSize);
            ParentAreaBuilder.AddImage(visual, null, new Color(0f, 0f, 0f, 0f), true);

            RectTransform backdrop = ParentAreaBuilder.NewRect("Backdrop", visual);
            ParentAreaBuilder.SetAnchored(backdrop, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * ButtonBackdropSize);
            ParentAreaBuilder.AddImage(backdrop, circle, BackdropColor, false);

            RectTransform disc = ParentAreaBuilder.NewRect("Disc", visual);
            ParentAreaBuilder.SetAnchored(disc, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * ButtonCircleSize);
            ParentAreaBuilder.AddImage(disc, circle, ButtonColor, false);

            RectTransform icon = ParentAreaBuilder.NewRect("Icon", visual);
            ParentAreaBuilder.SetAnchored(icon, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * ButtonIconSize);
            ParentAreaBuilder.AddImage(icon, arrow, Color.white, false);

            var button = root.gameObject.AddComponent<QuitButton>();
            PlaygroundPrototypeBuilder.SetObject(button, "prompt", prompt);
            PlaygroundPrototypeBuilder.SetObject(button, "visual", visual.gameObject);

            // Hidden until an activity runs (the button switches it on by itself).
            visual.gameObject.SetActive(false);
        }

        // ----- The prompt ------------------------------------------------------------------------

        static QuitPrompt BuildPrompt(Sprite circle, Sprite windowShape, Sprite arrow, Sprite tick, Sprite cross)
        {
            var canvasObject = new GameObject(PromptName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PromptSortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var audio = canvasObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.ignoreListenerPause = true;

            var prompt = canvasObject.AddComponent<QuitPrompt>();

            // Everything shown sits under "Root", which is switched off while the prompt is closed.
            RectTransform root = ParentAreaBuilder.NewStretched("Root", canvasObject.transform);

            // The dim takes every touch, so nothing behind the prompt (joystick, button, lessons) can be reached.
            ParentAreaBuilder.AddImage(ParentAreaBuilder.NewStretched("Dim", root), null, DimColor, true);

            RectTransform window = ParentAreaBuilder.NewRect("Window", root);
            ParentAreaBuilder.SetAnchored(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, WindowHeight));
            Image windowImage = ParentAreaBuilder.AddImage(window, windowShape, WindowColor, true);
            windowImage.type = Image.Type.Sliced;
            windowImage.preserveAspect = false;

            // The same arrow as on the button, big, so he knows what this is about.
            RectTransform arrowRect = ParentAreaBuilder.NewRect("Arrow", window);
            ParentAreaBuilder.SetAnchored(arrowRect, new Vector2(0.5f, 0.5f), new Vector2(0f, ArrowY), Vector2.one * ArrowSize);
            ParentAreaBuilder.AddImage(arrowRect, arrow, Color.white, false);

            Button continueButton = BuildChoice(window, "ContinueButton", new Vector2(-ChoiceX, ChoiceY), circle, cross, ContinueColor);
            Button stopButton = BuildChoice(window, "StopButton", new Vector2(ChoiceX, ChoiceY), circle, tick, StopColor);

            PlaygroundPrototypeBuilder.SetObject(prompt, "root", root.gameObject);
            PlaygroundPrototypeBuilder.SetObject(prompt, "stopButton", stopButton);
            PlaygroundPrototypeBuilder.SetObject(prompt, "continueButton", continueButton);
            PlaygroundPrototypeBuilder.SetObject(prompt, "window", window);
            PlaygroundPrototypeBuilder.SetObject(prompt, "voiceSource", audio);

            root.gameObject.SetActive(false);
            return prompt;
        }

        /// <summary>A big round coloured button with a white picture on it.</summary>
        static Button BuildChoice(RectTransform parent, string name, Vector2 position, Sprite circle, Sprite icon, Color color)
        {
            RectTransform rect = ParentAreaBuilder.NewRect(name, parent);
            ParentAreaBuilder.SetAnchored(rect, new Vector2(0.5f, 0.5f), position, Vector2.one * ChoiceSize);
            Image background = ParentAreaBuilder.AddImage(rect, circle, color, true);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            RectTransform iconRect = ParentAreaBuilder.NewRect("Icon", rect);
            ParentAreaBuilder.SetAnchored(iconRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * ChoiceIconSize);
            ParentAreaBuilder.AddImage(iconRect, icon, Color.white, false);
            return button;
        }
    }
}
