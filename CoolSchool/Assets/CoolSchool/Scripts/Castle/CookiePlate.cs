using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Input;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// Feeding the hungry dragon. The dragon says how many cookies it wants; he taps cookies on the plate and they fly into the bowl
    /// in front of the dragon while their numbers are said. The action button shows a green tick: tapping it means "enough!". Too few
    /// or too many cookies is never a failure: we count along what is in the bowl, or the extra ones fly back, and he tries again.
    /// RunQuick is the easy version: one big cookie, one tap.
    /// </summary>
    public class CookiePlate : MonoBehaviour
    {
        [Tooltip("10 small cookies lying on the plate.")]
        [SerializeField] Transform[] cookies;
        [Tooltip("One big cookie (0.4 m), hidden at the start, for the quick version.")]
        [SerializeField] Transform bigCookie;
        [Tooltip("Where the given cookies land, in front of the dragon.")]
        [SerializeField] Transform bowl;
        [SerializeField] WorldTapPicker picker;
        [SerializeField] ActionButton button;
        [Tooltip("The green tick shown on the action button while he feeds the dragon.")]
        [SerializeField] Sprite checkIcon;

        const float FlySeconds = 0.5f;
        const float HintSeconds = 15f;
        const float SolveSeconds = 60f;
        const float QuickSolveSeconds = 20f;
        const float CountGapSeconds = 0.85f;
        const float AutoGapSeconds = 1.0f;

        sealed class Home
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public bool Active;
        }

        readonly Dictionary<Transform, Home> homes = new Dictionary<Transform, Home>();
        readonly List<Transform> inBowl = new List<Transform>();
        readonly List<Transform> tapQueue = new List<Transform>();
        int flying;
        bool donePressed;
        bool buttonHooked;

        // ----- The full version ------------------------------------------------------------------

        public IEnumerator Run(DragonBoss dragon, int wanted, MomentResult result)
        {
            CacheHomes();
            int amount = Mathf.Clamp(wanted, 2, Mathf.Min(8, cookies != null ? cookies.Length : 8));
            if (cookies == null || cookies.Length < amount || bowl == null)
            {
                if (dragon != null) yield return dragon.Eat(bowl != null ? bowl.position : dragon.transform.position);
                yield break;
            }

            inBowl.Clear();
            tapQueue.Clear();
            flying = 0;
            donePressed = false;

            var available = new List<Transform>();
            foreach (Transform cookie in cookies)
                if (cookie != null)
                {
                    cookie.gameObject.SetActive(true);
                    available.Add(cookie);
                }

            // The dragon asks, then the hint tells him what to do.
            yield return CastleMotion.SayAndWait("castle.cookies_want." + amount);
            yield return CastleMotion.SayAndWait("castle.cookies_hint");

            HookButton(true);
            if (picker != null)
            {
                picker.SetTargets(available);
                picker.IgnorePress = PressIsOnButton;
                picker.Tapped += OnTapped;
                picker.Armed = true;
            }

            float lastActivity = Time.time;
            float nextHint = Time.time + HintSeconds;
            bool eaten = false;
            while (!eaten)
            {
                if (tapQueue.Count > 0)
                {
                    Transform cookie = tapQueue[0];
                    tapQueue.Clear();
                    lastActivity = Time.time;
                    nextHint = Time.time + HintSeconds;
                    if (cookie != null && !inBowl.Contains(cookie)) PutInBowl(cookie, available);
                }
                else if (donePressed)
                {
                    donePressed = false;
                    lastActivity = Time.time;
                    nextHint = Time.time + HintSeconds;
                    while (flying > 0) yield return null;

                    if (inBowl.Count == amount)
                    {
                        eaten = true;
                    }
                    else if (inBowl.Count < amount)
                    {
                        if (picker != null) picker.Armed = false;
                        if (inBowl.Count > 0 && result != null) result.Mistakes++;
                        yield return CastleMotion.SayAndWait("castle.cookies_more", true);
                        yield return CountBowl();
                        if (picker != null) picker.Armed = true;
                    }
                    else
                    {
                        if (picker != null) picker.Armed = false;
                        if (result != null) result.Mistakes++;
                        yield return CastleMotion.SayAndWait("castle.cookies_too_many", true);
                        yield return SendExtrasBack(amount, available);
                        yield return CountBowl();
                        if (picker != null) picker.Armed = true;
                    }
                    tapQueue.Clear();
                    donePressed = false;
                }
                else if (Time.time - lastActivity >= SolveSeconds)
                {
                    // Solved for him: the right amount ends up in the bowl, one by one, counted out loud.
                    if (picker != null) picker.Armed = false;
                    while (inBowl.Count > amount) yield return SendExtrasBack(amount, available);
                    while (inBowl.Count < amount)
                    {
                        Transform next = FirstFree(available);
                        if (next == null) break;

                        PutInBowl(next, available);
                        yield return new WaitForSeconds(AutoGapSeconds);
                    }
                    while (flying > 0) yield return null;
                    eaten = true;
                }
                else if (Time.time >= nextHint && CastleMotion.VoiceIsFree())
                {
                    nextHint = Time.time + HintSeconds;
                    VoicePlayer.Say("castle.cookies_hint");
                }
                yield return null;
            }

            // Hands off: the dragon eats what is in the bowl.
            if (picker != null)
            {
                picker.Armed = false;
                picker.Tapped -= OnTapped;
                picker.Clear();
            }
            HookButton(false);
            if (button != null) button.ResetIcon();

            if (dragon != null) yield return dragon.Eat(bowl.position);
            foreach (Transform cookie in inBowl)
            {
                if (cookie == null) continue;
                BattleEffects.Confetti(cookie.position, new Color(0.55f, 0.33f, 0.15f));
                cookie.gameObject.SetActive(false);
            }
            yield return CastleMotion.SayAndWait("castle.cookies_yum");
        }

        // ----- The quick version -----------------------------------------------------------------

        /// <summary>One big cookie appears; one tap and the dragon eats it. It flies in by itself after 20 s.</summary>
        public IEnumerator RunQuick(DragonBoss dragon)
        {
            CacheHomes();
            if (bigCookie == null)
            {
                if (dragon != null) yield return dragon.Eat(dragon.transform.position + dragon.transform.forward);
                yield break;
            }

            tapQueue.Clear();
            Home home = homes[bigCookie];
            bigCookie.gameObject.SetActive(true);
            BattleEffects.Sparkle(bigCookie.position + Vector3.up * 0.2f);
            this.Fork(CastleMotion.PopIn(bigCookie, home.Scale, 0.4f));
            yield return CastleMotion.SayAndWait("castle.cookie_big");

            if (picker != null)
            {
                picker.SetTargets(new[] { bigCookie });
                picker.Tapped += OnTapped;
                picker.Armed = true;
            }

            float started = Time.time;
            float nextHint = Time.time + HintSeconds;
            while (tapQueue.Count == 0 && Time.time - started < QuickSolveSeconds)
            {
                if (Time.time >= nextHint && CastleMotion.VoiceIsFree())
                {
                    nextHint = Time.time + HintSeconds;
                    VoicePlayer.Say("castle.cookie_big");
                }
                yield return null;
            }

            if (picker != null)
            {
                picker.Armed = false;
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            // It flies to the dragon's mouth while the dragon turns to it and eats.
            Vector3 mouth = dragon != null ? dragon.MouthPoint.position : bigCookie.position;
            this.Fork(FlyAndVanish(bigCookie, mouth));
            if (dragon != null) yield return dragon.Eat(mouth);
            yield return CastleMotion.SayAndWait("castle.cookies_yum");
        }

        /// <summary>Everything back on the plate and in its place, the button's picture back, the picker released. Safe at any moment.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            HookButton(false);
            if (button != null) button.ResetIcon();
            if (picker != null)
            {
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            inBowl.Clear();
            tapQueue.Clear();
            flying = 0;
            donePressed = false;

            foreach (KeyValuePair<Transform, Home> entry in homes)
            {
                if (entry.Key == null) continue;

                entry.Key.SetPositionAndRotation(entry.Value.Position, entry.Value.Rotation);
                entry.Key.localScale = entry.Value.Scale;
                entry.Key.gameObject.SetActive(entry.Value.Active);
            }
        }

        // ----- The bowl --------------------------------------------------------------------------

        void PutInBowl(Transform cookie, List<Transform> available)
        {
            inBowl.Add(cookie);
            available.Remove(cookie);
            if (picker != null) picker.Remove(cookie);

            int number = inBowl.Count;
            VoicePlayer.Say("number." + number, true);
            this.Fork(FlyToBowl(cookie, BowlSpot(number - 1)));
        }

        IEnumerator FlyToBowl(Transform cookie, Vector3 spot)
        {
            flying++;
            yield return CastleMotion.Arc(cookie, spot, FlySeconds, 1.0f);
            if (cookie != null)
            {
                Sfx.Play("castle.block_place", spot);
                BattleEffects.Sparkle(spot + Vector3.up * 0.1f);
            }
            flying--;
        }

        /// <summary>Cookies sit in the bowl in a little ring, a second layer on top when there are many.</summary>
        Vector3 BowlSpot(int index)
        {
            int perLayer = 5;
            int layer = index / perLayer;
            int place = index % perLayer;
            float angle = place * Mathf.PI * 2f / perLayer + layer * 0.6f;
            float radius = layer == 0 ? 0.18f : 0.08f;
            return bowl.position + new Vector3(Mathf.Cos(angle) * radius, 0.08f + layer * 0.06f, Mathf.Sin(angle) * radius);
        }

        /// <summary>The cookies in the bowl hop one by one while their numbers are said.</summary>
        IEnumerator CountBowl()
        {
            for (int k = 0; k < inBowl.Count; k++)
            {
                if (inBowl[k] == null) continue;

                VoicePlayer.Say("number." + (k + 1), true);
                this.Fork(CastleMotion.Hop(inBowl[k], 0.25f, 0.35f));
                yield return new WaitForSeconds(CountGapSeconds);
            }
            yield return CastleMotion.WaitForVoice(3f);
        }

        /// <summary>The cookies beyond the wanted amount fly back to the plate, the last one first, one at a time.</summary>
        IEnumerator SendExtrasBack(int amount, List<Transform> available)
        {
            while (inBowl.Count > amount)
            {
                Transform extra = inBowl[inBowl.Count - 1];
                inBowl.RemoveAt(inBowl.Count - 1);
                if (extra == null) continue;

                available.Add(extra);
                if (picker != null) picker.SetTargets(available);
                Home home;
                Vector3 target = homes.TryGetValue(extra, out home) ? home.Position : extra.position;
                this.Fork(FlyBack(extra, target));
                yield return new WaitForSeconds(0.5f);
            }
            while (flying > 0) yield return null;
        }

        IEnumerator FlyBack(Transform cookie, Vector3 target)
        {
            flying++;
            yield return CastleMotion.Arc(cookie, target, 0.45f, 0.9f);
            flying--;
        }

        IEnumerator FlyAndVanish(Transform cookie, Vector3 mouth)
        {
            yield return CastleMotion.Arc(cookie, mouth, 0.6f, 0.6f);
            if (cookie != null)
            {
                BattleEffects.Confetti(mouth, new Color(0.55f, 0.33f, 0.15f));
                cookie.gameObject.SetActive(false);
            }
        }

        static Transform FirstFree(List<Transform> available)
        {
            foreach (Transform cookie in available)
                if (cookie != null) return cookie;
            return null;
        }

        // ----- Input -----------------------------------------------------------------------------

        void OnTapped(Transform tapped)
        {
            if (tapped != null && !tapQueue.Contains(tapped)) tapQueue.Add(tapped);
        }

        void OnButtonPressed()
        {
            donePressed = true;
        }

        void HookButton(bool on)
        {
            if (button == null) return;

            if (on && !buttonHooked)
            {
                button.Pressed += OnButtonPressed;
                buttonHooked = true;
                if (checkIcon != null) button.SetIcon(checkIcon);
            }
            else if (!on && buttonHooked)
            {
                button.Pressed -= OnButtonPressed;
                buttonHooked = false;
            }
        }

        /// <summary>A press on the action button is "enough", not a tap on a cookie.</summary>
        bool PressIsOnButton(Vector2 screenPoint)
        {
            if (button == null) return false;

            var rect = button.transform as RectTransform;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
        }

        void CacheHomes()
        {
            if (homes.Count > 0) return;

            if (cookies != null)
                foreach (Transform cookie in cookies)
                    Remember(cookie);
            Remember(bigCookie);
        }

        void Remember(Transform thing)
        {
            if (thing == null) return;

            homes[thing] = new Home
            {
                Position = thing.position,
                Rotation = thing.rotation,
                Scale = thing.localScale,
                Active = thing.gameObject.activeSelf,
            };
        }
    }
}
