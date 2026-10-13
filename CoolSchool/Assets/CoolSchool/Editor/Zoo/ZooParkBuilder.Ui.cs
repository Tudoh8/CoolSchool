using CoolSchool.Zoo;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The two screen pieces of hide and seek on the touch canvas: the checklist (a round panel with five round backs and icon
    /// images, top centre like the treasure map) and the "eyes closed" overlay (a full-screen dark veil with a big digit). Pictures and
    /// digits only. The component scripts do the animation, the peek holes and the shadow of the digit themselves.
    /// </summary>
    internal static partial class ZooParkBuilder
    {
        const string CanvasName = "TouchUI";
        const int ChecklistSlots = 5;
        const float SlotSpacing = 138f;

        static readonly Color PanelColour = new Color(0.1f, 0.34f, 0.2f, 0.92f);
        static readonly Color SlotBackColour = new Color(1f, 0.97f, 0.86f, 1f);

        // ---- The checklist ----

        /// <summary>
        /// A holder (never blocks touches) with the ZooChecklistUI on it and a "Panel" child at the top centre, 24 px from the top:
        /// five round backs 138 px apart, each with its icon image inside. The panel starts switched off.
        /// </summary>
        static ZooChecklistUI BuildChecklist()
        {
            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas == null)
            {
                Debug.LogWarning($"CoolSchool zoo park: no '{CanvasName}' canvas found, so the checklist was not built.");
                return null;
            }

            Sprite background = ShopAssets.FindShape("Background");
            Sprite circle = ShopAssets.FindShape("Circle");

            RectTransform holder = UiRect("ZooChecklist", canvas.transform);
            UiStretch(holder);
            var checklist = holder.gameObject.AddComponent<ZooChecklistUI>();

            RectTransform panel = UiRect("Panel", holder);
            panel.anchorMin = new Vector2(0.5f, 1f);
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -24f);
            panel.sizeDelta = new Vector2(ChecklistSlots * SlotSpacing + 28f, 156f);
            UiFill(UiImage(panel, background, PanelColour));

            var slots = new Image[ChecklistSlots];
            var backs = new Image[ChecklistSlots];
            for (int i = 0; i < ChecklistSlots; i++)
            {
                RectTransform back = UiRect("Back_" + (i + 1), panel);
                UiCentred(back, new Vector2(0f, 0f), new Vector2(124f, 124f));
                back.anchoredPosition = new Vector2((i - (ChecklistSlots - 1) * 0.5f) * SlotSpacing, 0f);
                backs[i] = UiImage(back, circle, SlotBackColour);

                RectTransform icon = UiRect("Icon_" + (i + 1), back);
                UiCentred(icon, Vector2.zero, new Vector2(98f, 98f));
                slots[i] = UiImage(icon, null, Color.black);
            }

            SetObject(checklist, "panel", panel);
            SetObjectArray(checklist, "slots", slots);
            SetObjectArray(checklist, "slotBacks", backs);
            panel.gameObject.SetActive(false);
            return checklist;
        }

        // ---- The eyes-closed overlay ----

        /// <summary>
        /// A full-screen dark veil (alpha 0 until the count) with a CanvasGroup and a big white Inter-Bold digit in the middle. It is
        /// built after the HUD elements that exist at this point and before the parent gear, the back button and the present popup, so
        /// those stay on top of it.
        /// </summary>
        static EyesClosedOverlay BuildEyesOverlay()
        {
            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas == null)
            {
                Debug.LogWarning($"CoolSchool zoo park: no '{CanvasName}' canvas found, so the eyes-closed overlay was not built.");
                return null;
            }

            RectTransform veil = UiRect("EyesClosedOverlay", canvas.transform);
            UiStretch(veil);
            UiImage(veil, null, new Color(0.03f, 0.025f, 0.07f, 1f));
            var group = veil.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Font font = FindFont("Inter-Bold");
            RectTransform digitRect = UiRect("Digit", veil);
            UiCentred(digitRect, new Vector2(0f, 20f), new Vector2(900f, 640f));
            var digit = digitRect.gameObject.AddComponent<Text>();
            digit.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            digit.fontSize = 440;
            digit.fontStyle = FontStyle.Bold;
            digit.color = Color.white;
            digit.alignment = TextAnchor.MiddleCenter;
            digit.horizontalOverflow = HorizontalWrapMode.Overflow;
            digit.verticalOverflow = VerticalWrapMode.Overflow;
            digit.raycastTarget = false;
            digit.text = string.Empty;

            var overlay = veil.gameObject.AddComponent<EyesClosedOverlay>();
            SetObject(overlay, "group", group);
            SetObject(overlay, "digit", digit);
            return overlay;
        }

        /// <summary>The font with this file name (null if it is not in the project).</summary>
        static Font FindFont(string fontName)
        {
            foreach (string guid in AssetDatabase.FindAssets(fontName + " t:Font"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != fontName) continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null) return font;
            }
            return null;
        }

        // ---- Small UI helpers ----

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

        /// <summary>Anchored to the middle of its parent, at an offset, with a fixed size.</summary>
        static void UiCentred(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
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

        /// <summary>Makes a panel picture fill its rect: no kept aspect, and sliced (rounded corners stay round) when the sprite has borders.</summary>
        static void UiFill(Image image)
        {
            image.preserveAspect = false;
            if (image.sprite != null && image.sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        }
    }
}
