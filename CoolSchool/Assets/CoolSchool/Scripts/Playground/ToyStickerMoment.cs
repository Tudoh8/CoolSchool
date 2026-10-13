using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The sticker moment after a first play on a toy: a round sticker (the toy's picture on a white disc with a coloured rim) pops up
    /// above the kid with a sparkle and spins once, the narrator tells what it is, and the sticker flies to the star jar and fades out.
    /// All of it is drawn on a screen overlay canvas made on the spot.
    /// </summary>
    internal static class ToyStickerMoment
    {
        const float PopSeconds = 0.5f;
        const float SpinSeconds = 0.9f;
        const float MinShowSeconds = 1.3f;
        const float MaxVoiceWaitSeconds = 3.5f;
        const float FlightSeconds = 0.75f;
        const float HeadHeightMeters = 1.9f;
        const float RiseMeters = 0.5f;
        const float SizeFraction = 0.17f;   // of the screen height

        static Canvas canvas;

        /// <summary>Runs the whole moment. followPoint gives the world point of the kid's head each frame.</summary>
        public static IEnumerator Run(PlaygroundToy toy, Transform kid)
        {
            if (toy == null || kid == null) yield break;

            string id = toy.StickerId;
            Sprite icon = toy.StickerIcon;
            Camera camera = Camera.main;

            RectTransform sticker = Build(id, icon);
            Vector3 start = kid.position + Vector3.up * HeadHeightMeters;
            BattleEffects.Sparkle(start);
            Sfx.Play("toys.sticker_pop", start);

            // Pop up above the kid's head and spin once.
            for (float elapsed = 0f; elapsed < PopSeconds && sticker != null; elapsed += Time.deltaTime)
            {
                float k = elapsed / PopSeconds;
                Place(sticker, camera, kid, k * RiseMeters);
                float size = EaseOutBack(k);
                float spin = Mathf.Clamp01(elapsed / SpinSeconds) * 360f;
                sticker.localScale = Vector3.one * size;
                sticker.localRotation = Quaternion.Euler(0f, spin, 0f);
                yield return null;
            }

            // The narrator says what it is while the sticker floats and finishes its spin.
            VoicePlayer.Say("toys.sticker." + toy.VoiceName);
            float shown = PopSeconds;
            float voiceGiveUp = Time.time + MaxVoiceWaitSeconds;
            while (sticker != null && (shown < MinShowSeconds || (VoicePlayer.Instance != null && VoicePlayer.Instance.IsBusy && Time.time < voiceGiveUp)))
            {
                shown += Time.deltaTime;
                Place(sticker, camera, kid, RiseMeters + Mathf.Sin(Time.time * 4f) * 0.05f);
                float spin = Mathf.Clamp01(shown / SpinSeconds) * 360f;
                sticker.localScale = Vector3.one;
                sticker.localRotation = Quaternion.Euler(0f, spin >= 360f ? 0f : spin, 0f);
                yield return null;
            }

            // Fly to the star jar and fade.
            if (sticker != null)
            {
                Vector2 from = sticker.position;
                Vector2 to = JarScreenPoint();
                Vector2 control = (from + to) * 0.5f + Vector2.up * (Screen.height * 0.12f);
                var images = sticker.GetComponentsInChildren<Image>();
                Sfx.Play("toys.sticker_stick");
                for (float elapsed = 0f; elapsed < FlightSeconds; elapsed += Time.deltaTime)
                {
                    if (sticker == null) break;

                    float k = Mathf.SmoothStep(0f, 1f, elapsed / FlightSeconds);
                    float u = 1f - k;
                    sticker.position = u * u * from + 2f * u * k * control + k * k * to;
                    sticker.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, k);
                    sticker.localRotation = Quaternion.Euler(0f, 0f, 360f * k);
                    foreach (Image image in images) if (image != null) image.color = WithAlpha(image.color, k > 0.8f ? (1f - k) / 0.2f : 1f);
                    yield return null;
                }
                if (sticker != null) Object.Destroy(sticker.gameObject);
            }
        }

        // ---- Pieces ----

        static RectTransform Build(string id, Sprite icon)
        {
            EnsureCanvas();
            float size = Screen.height * SizeFraction;

            var root = new GameObject("ToySticker", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = Vector3.zero;

            AddImage(rect, "Rim", ToyStickerArt.Disc, ToyStickerArt.RimColour(id), size);
            AddImage(rect, "Face", ToyStickerArt.Disc, Color.white, size * 0.84f);
            if (icon != null) AddImage(rect, "Icon", icon, Color.white, size * 0.66f);
            return rect;
        }

        static void AddImage(RectTransform parent, string name, Sprite sprite, Color colour, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size, size);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        static void EnsureCanvas()
        {
            if (canvas != null) return;

            var go = new GameObject("ToyStickerCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            Object.DontDestroyOnLoad(go);
        }

        static void Place(RectTransform sticker, Camera camera, Transform kid, float rise)
        {
            Vector3 world = kid.position + Vector3.up * (HeadHeightMeters + rise);
            sticker.position = camera != null ? (Vector3)camera.WorldToScreenPoint(world) : new Vector3(Screen.width * 0.5f, Screen.height * 0.6f);
        }

        static Vector2 JarScreenPoint()
        {
            StarJarHud hud = Object.FindFirstObjectByType<StarJarHud>();
            if (hud == null) return new Vector2(Screen.width * 0.92f, Screen.height * 0.9f);

            Canvas hudCanvas = hud.GetComponentInParent<Canvas>();
            Camera uiCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? hudCanvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(uiCamera, hud.transform.position);
        }

        static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);
            return colour;
        }

        static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float t = Mathf.Clamp01(k) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}
