using System.Collections;
using CoolSchool.Lessons;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// The checklist at the top centre of the screen during hide and seek: one round back with a black silhouette for every animal
    /// that is hiding. When one is found its silhouette turns into the colour picture with a pop, so he can see at a glance how
    /// many are still hiding (and count the black ones). Pictures only, no words. Only as many slots as there are hiders are shown,
    /// centred, and the panel narrows to fit them.
    /// </summary>
    public class ZooChecklistUI : MonoBehaviour
    {
        [Tooltip("The panel behind the slots. Must be this object or a child of it: it is switched off and on.")]
        [SerializeField] RectTransform panel;
        [Tooltip("The 5 icon images, left to right. Black while the animal is still hiding.")]
        [SerializeField] Image[] slots;
        [Tooltip("The 5 round backs behind the icons.")]
        [SerializeField] Image[] slotBacks;

        const float AppearSeconds = 0.35f;
        const float HideSeconds = 0.2f;
        const float FoundPopSeconds = 0.45f;
        const float FoundStartScale = 1.7f;
        const float PulseSeconds = 0.7f;
        const float PulseExtraScale = 0.45f;
        const float CountStepSeconds = 0.9f;
        const float HopSeconds = 0.5f;
        const float HopHeight = 34f;
        const float HopStagger = 0.09f;
        static readonly Color SilhouetteColor = Color.black;
        static readonly Color FoundColor = Color.white;

        // What a slot is doing: nothing, popping into colour, pulsing for the count, or hopping in the celebration.
        enum Motion { None, Pop, Pulse, Hop }

        int shown;
        bool[] found;
        Motion[] motion;
        float[] motionStart;
        // Where each slot and back stood in the scene, where they rest after the centring, and how big they are.
        Vector2[] slotOriginal;
        Vector2[] backOriginal;
        Vector2[] slotRest;
        Vector2[] backRest;
        Vector3[] slotBaseScale;
        Vector3[] backBaseScale;
        bool[] slotInBack;
        Vector2 panelBaseSize;
        Vector3 panelBaseScale = Vector3.one;
        Coroutine panelRoutine;
        bool captured;

        int SlotCount => slots != null ? slots.Length : 0;

        void Awake()
        {
            Capture();
            if (panel != null && panel.gameObject != gameObject) panel.gameObject.SetActive(false);
        }

        void Capture()
        {
            if (captured) return;

            captured = true;
            int count = SlotCount;
            found = new bool[count];
            motion = new Motion[count];
            motionStart = new float[count];
            slotOriginal = new Vector2[count];
            backOriginal = new Vector2[count];
            slotRest = new Vector2[count];
            backRest = new Vector2[count];
            slotBaseScale = new Vector3[count];
            backBaseScale = new Vector3[count];
            slotInBack = new bool[count];

            for (int i = 0; i < count; i++)
            {
                Image back = BackOf(i);
                if (slots[i] != null)
                {
                    slotOriginal[i] = slots[i].rectTransform.anchoredPosition;
                    slotRest[i] = slotOriginal[i];
                    slotBaseScale[i] = slots[i].rectTransform.localScale;
                    slotInBack[i] = back != null && slots[i].transform.IsChildOf(back.transform);
                }
                if (back != null)
                {
                    backOriginal[i] = back.rectTransform.anchoredPosition;
                    backRest[i] = backOriginal[i];
                    backBaseScale[i] = back.rectTransform.localScale;
                }
            }

            if (panel != null)
            {
                panelBaseSize = panel.sizeDelta;
                panelBaseScale = panel.localScale;
            }
        }

        Image BackOf(int i)
        {
            return slotBacks != null && i < slotBacks.Length ? slotBacks[i] : null;
        }

        // ----- The calls ---------------------------------------------------------------------------

        /// <summary>Shows one black silhouette for every picture (up to the number of slots).</summary>
        public void Show(Sprite[] icons)
        {
            Capture();
            if (panel == null) return;

            shown = icons != null ? Mathf.Min(icons.Length, SlotCount) : 0;
            LayOut();

            for (int i = 0; i < SlotCount; i++)
            {
                bool used = i < shown;
                found[i] = false;
                motion[i] = Motion.None;

                if (slots[i] != null)
                {
                    slots[i].gameObject.SetActive(used);
                    if (used)
                    {
                        slots[i].sprite = icons[i];
                        slots[i].preserveAspect = true;
                        slots[i].color = SilhouetteColor;
                    }
                }
                Image back = BackOf(i);
                if (back != null) back.gameObject.SetActive(used);
                ApplyPose(i, 0f, 1f);
            }

            panel.gameObject.SetActive(true);
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            if (isActiveAndEnabled)
                panelRoutine = StartCoroutine(Appear());
            else
                panel.localScale = panelBaseScale;
        }

        /// <summary>The animal at this index is found: its silhouette turns into the colour picture, with a pop.</summary>
        public void SetFound(int index)
        {
            Capture();
            if (index < 0 || index >= shown || found[index]) return;

            found[index] = true;
            if (slots[index] != null) slots[index].color = FoundColor;
            Begin(index, Motion.Pop);
        }

        /// <summary>
        /// Counts the silhouettes that are still black: each one pulses in turn while the voice says its number. Used for the
        /// "how many are left" question, when he is wrong. Wait for it with yield return.
        /// </summary>
        public IEnumerator CountSilhouettes()
        {
            Capture();
            int number = 0;
            for (int i = 0; i < shown; i++)
            {
                if (found[i]) continue;

                number++;
                Begin(i, Motion.Pulse);
                VoicePlayer.Say("number." + number, true);
                yield return new WaitForSeconds(CountStepSeconds);
            }
        }

        /// <summary>Every slot hops once, one after the other.</summary>
        public void Celebrate()
        {
            Capture();
            for (int i = 0; i < shown; i++)
                Begin(i, Motion.Hop, i * HopStagger);
        }

        /// <summary>Shrinks the panel away.</summary>
        public void Hide()
        {
            Capture();
            if (panel == null) return;

            if (panelRoutine != null)
            {
                StopCoroutine(panelRoutine);
                panelRoutine = null;
            }
            for (int i = 0; i < SlotCount; i++) motion[i] = Motion.None;
            shown = 0;

            if (!isActiveAndEnabled || !panel.gameObject.activeSelf)
            {
                panel.localScale = panelBaseScale;
                if (panel.gameObject != gameObject) panel.gameObject.SetActive(false);
                return;
            }
            panelRoutine = StartCoroutine(Vanish());
        }

        // ----- Layout ------------------------------------------------------------------------------

        /// <summary>
        /// Centres the slots that are used (the middle of the builder's row stays the middle) and narrows the panel by the
        /// room of the ones that are not. Always starts from the places the builder gave the slots, so it can run every time.
        /// </summary>
        void LayOut()
        {
            int count = SlotCount;
            if (count == 0) return;

            // The distance between two neighbouring slots as the builder laid them out.
            float spacing = 0f;
            if (count >= 2) spacing = OriginalX(1) - OriginalX(0);

            if (panel != null && Mathf.Approximately(panel.anchorMin.x, panel.anchorMax.x))
            {
                float narrower = Mathf.Abs(spacing) * (count - shown);
                panel.sizeDelta = new Vector2(Mathf.Max(Mathf.Abs(spacing), panelBaseSize.x - narrower), panelBaseSize.y);
            }

            float middle = 0f;
            for (int i = 0; i < count; i++) middle += OriginalX(i);
            middle /= count;

            for (int i = 0; i < count; i++)
            {
                float shift = middle + (i - (shown - 1) * 0.5f) * spacing - OriginalX(i);
                slotRest[i] = new Vector2(slotOriginal[i].x + shift, slotOriginal[i].y);
                backRest[i] = new Vector2(backOriginal[i].x + shift, backOriginal[i].y);
            }
        }

        /// <summary>Where slot i stood in the scene: its back when it has one, else the icon.</summary>
        float OriginalX(int i)
        {
            return BackOf(i) != null ? backOriginal[i].x : slotOriginal[i].x;
        }

        // ----- Animation ---------------------------------------------------------------------------

        void Begin(int index, Motion kind, float delay = 0f)
        {
            motion[index] = kind;
            motionStart[index] = Time.time + delay;
        }

        void Update()
        {
            if (panel == null || !panel.gameObject.activeSelf) return;

            for (int i = 0; i < shown; i++)
            {
                float lift = 0f;
                float scale = 1f;

                if (motion[i] != Motion.None)
                {
                    float age = Time.time - motionStart[i];
                    switch (motion[i])
                    {
                        case Motion.Pop:
                            if (age >= FoundPopSeconds) motion[i] = Motion.None;
                            else if (age >= 0f) scale = Mathf.LerpUnclamped(FoundStartScale, 1f, LessonMotion.OutBack(age / FoundPopSeconds));
                            break;
                        case Motion.Pulse:
                            if (age >= PulseSeconds) motion[i] = Motion.None;
                            else if (age >= 0f) scale = 1f + PulseExtraScale * LessonMotion.Hump(age / PulseSeconds);
                            break;
                        case Motion.Hop:
                            if (age >= HopSeconds) motion[i] = Motion.None;
                            else if (age >= 0f) lift = HopHeight * LessonMotion.Hump(age / HopSeconds);
                            break;
                    }
                }

                ApplyPose(i, lift, scale);
            }
        }

        /// <summary>Puts a slot (and its back) at its resting place, lifted and scaled.</summary>
        void ApplyPose(int i, float lift, float scale)
        {
            Image back = BackOf(i);
            Vector2 up = new Vector2(0f, lift);
            if (back != null)
            {
                back.rectTransform.anchoredPosition = backRest[i] + up;
                back.rectTransform.localScale = backBaseScale[i] * scale;
            }
            if (slots[i] == null) return;

            // A slot inside its back follows it (position and scale) without being moved on its own.
            if (slotInBack[i]) return;

            slots[i].rectTransform.anchoredPosition = slotRest[i] + up;
            slots[i].rectTransform.localScale = slotBaseScale[i] * scale;
        }

        IEnumerator Appear()
        {
            for (float elapsed = 0f; elapsed < AppearSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / AppearSeconds;
                panel.localScale = panelBaseScale * Mathf.Max(0.01f, LessonMotion.OutBack(t));
                yield return null;
            }

            panel.localScale = panelBaseScale;
            panelRoutine = null;
        }

        IEnumerator Vanish()
        {
            Vector3 from = panel.localScale;
            for (float elapsed = 0f; elapsed < HideSeconds; elapsed += Time.deltaTime)
            {
                panel.localScale = Vector3.Lerp(from, Vector3.zero, Mathf.SmoothStep(0f, 1f, elapsed / HideSeconds));
                yield return null;
            }

            panel.localScale = panelBaseScale;
            if (panel.gameObject != gameObject) panel.gameObject.SetActive(false);
            panelRoutine = null;
        }

        void OnDisable()
        {
            // Switched off mid-animation: leave the panel tidy for next time.
            panelRoutine = null;
            if (panel != null && captured)
            {
                panel.localScale = panelBaseScale;
                if (panel.gameObject != gameObject) panel.gameObject.SetActive(false);
            }
        }
    }
}
