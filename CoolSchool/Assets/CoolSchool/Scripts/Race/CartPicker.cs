using CoolSchool.Audio;
using CoolSchool.Parent;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Race
{
    /// <summary>
    /// The cart picker at the start of the soapbox race: three cart bodies and three extras (flag, stickers, gold paint) as big
    /// picture cards along the bottom of the screen. A body or extra he has won shows its picture; one he has not won yet shows a
    /// black silhouette with a lock. He taps to choose (the cart in front of him changes at once), then taps the green check, or
    /// waits: after 20 seconds without a tap the race starts with what is chosen. No words, he can't read.
    /// The component sits on a holder that stays active (so Update keeps running); everything it shows is under <c>root</c>.
    /// Each card is a Button with optional children named "Icon" (the picture), "Ring" (the gold ring) and "Lock" (the badge).
    /// </summary>
    public class CartPicker : MonoBehaviour
    {
        [Tooltip("A child of this object that is switched on while the picker is open.")]
        [SerializeField] GameObject root;
        [Tooltip("3, big: cart.racer_1, cart.racer_2 and cart.racer_3 (CartLooks.BodyIds).")]
        [SerializeField] Button[] bodyCards;
        [Tooltip("3, smaller: cart.flag, cart.stickers and cart.gold.")]
        [SerializeField] Button[] extraToggles;
        [Tooltip("Big green check, bottom right.")]
        [SerializeField] Button goButton;
        [Tooltip("Where the pictures come from.")]
        [SerializeField] RewardCatalog catalog;

        static readonly string[] ExtraIds = { CartLooks.FlagId, CartLooks.StickersId, CartLooks.GoldId };

        const float NagSeconds = 10f;
        const float AutoStartSeconds = 20f;
        const float LockedLineGapSeconds = 6f;
        const float IgnoreGoSeconds = 0.5f;
        const float PopInSeconds = 0.35f;
        const float PopInStagger = 0.06f;
        const float PunchSeconds = 0.3f;
        const float WobbleSeconds = 0.45f;
        const float SelectedScale = 1.12f;
        const float PulseHertz = 0.7f;

        static readonly Color OffColour = new Color(1f, 1f, 1f, 0.5f);

        // Cards 0..2 are the bodies, 3..5 the extras.
        const int CardCount = 6;
        readonly float[] punchStart = new float[CardCount];
        readonly float[] wobbleStart = new float[CardCount];

        int body;
        bool flagOn;
        bool stickersOn;
        bool goldOn;
        System.Action<int, bool, bool, bool> onChanged;
        System.Action onDone;

        float openedAt;
        float idleSeconds;
        bool nagged;
        float lastLockedLine = -100f;

        /// <summary>True while the cards are on the screen.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The body that is chosen now (0..2).</summary>
        public int Body => body;

        void Awake()
        {
            for (int i = 0; i < CardCount; i++)
            {
                punchStart[i] = -100f;
                wobbleStart[i] = -100f;
            }

            if (bodyCards != null)
            {
                for (int i = 0; i < bodyCards.Length; i++)
                {
                    int index = i;
                    if (bodyCards[i] != null) bodyCards[i].onClick.AddListener(() => TapBody(index));
                }
            }
            if (extraToggles != null)
            {
                for (int i = 0; i < extraToggles.Length; i++)
                {
                    int index = i;
                    if (extraToggles[i] != null) extraToggles[i].onClick.AddListener(() => TapExtra(index));
                }
            }
            if (goButton != null) goButton.onClick.AddListener(TapGo);

            if (root != null && !IsOpen) root.SetActive(false);
        }

        void OnDisable()
        {
            // Never leave the cards up if this object is switched off.
            if (IsOpen) Close();
        }

        /// <summary>Opens with what he chose last time selected. See the other overload.</summary>
        public void Open(System.Action<int, bool, bool, bool> changed, System.Action done)
        {
            int start = Mathf.Max(0, System.Array.IndexOf(CartLooks.BodyIds, ProgressStore.ChosenCartId()));
            Open(start, ProgressStore.CartExtraOn(CartLooks.FlagId), ProgressStore.CartExtraOn(CartLooks.StickersId),
                ProgressStore.CartExtraOn(CartLooks.GoldId), changed, done);
        }

        /// <summary>
        /// Opens with this selection. <paramref name="changed"/> is called with (body, flag, stickers, gold) on every change, for the
        /// live preview; <paramref name="done"/> when he taps the check or after the auto-start.
        /// </summary>
        public void Open(int startBody, bool startFlag, bool startStickers, bool startGold,
            System.Action<int, bool, bool, bool> changed, System.Action done)
        {
            body = Mathf.Clamp(startBody, 0, CartLooks.BodyIds.Length - 1);
            // Extras that are not owned can't be on.
            flagOn = startFlag && Owned(ExtraIds[0]);
            stickersOn = startStickers && Owned(ExtraIds[1]);
            goldOn = startGold && Owned(ExtraIds[2]);
            onChanged = changed;
            onDone = done;

            IsOpen = true;
            openedAt = Time.unscaledTime;
            idleSeconds = 0f;
            nagged = false;
            for (int i = 0; i < CardCount; i++)
            {
                punchStart[i] = -100f;
                wobbleStart[i] = -100f;
            }

            if (root != null) root.SetActive(true);
            Refresh(true);
        }

        /// <summary>Hides the cards and forgets the callbacks. Safe to call at any moment.</summary>
        public void Close()
        {
            IsOpen = false;
            onChanged = null;
            onDone = null;
            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;

            // The quit prompt and the parent panel freeze the game: the timers wait too.
            if (!ParentGate.InputBlocked)
            {
                idleSeconds += Time.unscaledDeltaTime;
                if (idleSeconds >= NagSeconds && !nagged)
                {
                    nagged = true;
                    VoicePlayer.Say("race.pick_cart");
                }
                if (idleSeconds >= AutoStartSeconds)
                {
                    Finish();
                    return;
                }
            }

            Refresh(false);
        }

        // ----- Taps ---------------------------------------------------------------------------------

        void TapBody(int index)
        {
            if (!IsOpen || ParentGate.InputBlocked) return;
            Touched();

            if (!Owned(CartLooks.BodyIds[index]))
            {
                Locked(index);
                return;
            }

            body = index;
            Punch(index);
            Sfx.Play("ui.pick");
            Changed();
        }

        void TapExtra(int index)
        {
            if (!IsOpen || ParentGate.InputBlocked) return;
            Touched();

            if (!Owned(ExtraIds[index]))
            {
                Locked(BodyCount + index);
                return;
            }

            switch (index)
            {
                case 0: flagOn = !flagOn; break;
                case 1: stickersOn = !stickersOn; break;
                default: goldOn = !goldOn; break;
            }
            Punch(BodyCount + index);
            Sfx.Play("ui.pick");
            Changed();
        }

        void TapGo()
        {
            if (!IsOpen || ParentGate.InputBlocked) return;
            if (Time.unscaledTime - openedAt < IgnoreGoSeconds) return;
            Finish();
        }

        void Touched()
        {
            idleSeconds = 0f;
            nagged = false;
        }

        void Locked(int card)
        {
            wobbleStart[card] = Time.unscaledTime;
            Sfx.Play("ui.locked");
            if (Time.unscaledTime - lastLockedLine >= LockedLineGapSeconds)
            {
                lastLockedLine = Time.unscaledTime;
                VoicePlayer.Say("race.cart_locked", true);
            }
        }

        void Punch(int card)
        {
            punchStart[card] = Time.unscaledTime;
        }

        void Changed()
        {
            onChanged?.Invoke(body, flagOn, stickersOn, goldOn);
            Refresh(false);
        }

        void Finish()
        {
            System.Action done = onDone;
            Close();
            done?.Invoke();
        }

        // ----- Looks --------------------------------------------------------------------------------

        int BodyCount => bodyCards != null ? bodyCards.Length : 0;

        /// <summary>Starters are always owned; everything else once it has been won.</summary>
        static bool Owned(string id)
        {
            if (id == EquipSlots.StarterCartId) return true;
            PlayerProgress progress = ProgressStore.Current;
            return progress != null && progress.IsUnlocked(id);
        }

        /// <summary>Sets the picture, ring, lock badge, dimming and scale of every card.</summary>
        void Refresh(bool pictures)
        {
            if (bodyCards != null)
            {
                for (int i = 0; i < bodyCards.Length && i < CartLooks.BodyIds.Length; i++)
                    RefreshCard(bodyCards[i], i, CartLooks.BodyIds[i], i == body, pictures);
            }
            if (extraToggles != null)
            {
                bool[] on = { flagOn, stickersOn, goldOn };
                for (int i = 0; i < extraToggles.Length && i < ExtraIds.Length; i++)
                    RefreshCard(extraToggles[i], BodyCount + i, ExtraIds[i], on[i], pictures);
            }
        }

        void RefreshCard(Button card, int cardIndex, string itemId, bool chosen, bool pictures)
        {
            if (card == null) return;

            bool owned = Owned(itemId);
            Transform icon = card.transform.Find("Icon");
            Transform ring = card.transform.Find("Ring");
            Transform lockBadge = card.transform.Find("Lock");

            if (icon != null)
            {
                var image = icon.GetComponent<Image>();
                if (image != null)
                {
                    if (pictures)
                    {
                        RewardItem item = catalog != null ? catalog.Find(itemId) : null;
                        if (item != null && item.icon != null) image.sprite = item.icon;
                    }
                    // Locked: the black silhouette. Owned: white; an extra that is off is dimmed.
                    image.color = !owned ? Color.black : (chosen ? Color.white : (cardIndex >= BodyCount ? OffColour : Color.white));
                }
            }
            if (ring != null) ring.gameObject.SetActive(owned && chosen);
            if (lockBadge != null) lockBadge.gameObject.SetActive(!owned);

            float now = Time.unscaledTime;
            float scale = owned && chosen ? SelectedScale : 1f;

            // The chosen card breathes a little.
            if (owned && chosen) scale *= 1f + 0.03f * Mathf.Sin(now * Mathf.PI * 2f * PulseHertz);

            // Pop in, one after the other.
            float popT = Mathf.Clamp01((now - openedAt - cardIndex * PopInStagger) / PopInSeconds);
            scale *= EaseOutBack(popT);

            // A scale punch when tapped.
            float punchT = (now - punchStart[cardIndex]) / PunchSeconds;
            if (punchT >= 0f && punchT < 1f) scale *= 1f + 0.22f * Mathf.Sin(punchT * Mathf.PI);

            float angle = 0f;
            float wobbleT = (now - wobbleStart[cardIndex]) / WobbleSeconds;
            if (wobbleT >= 0f && wobbleT < 1f) angle = Mathf.Sin(wobbleT * Mathf.PI * 6f) * 9f * (1f - wobbleT);

            card.transform.localScale = Vector3.one * scale;
            card.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
