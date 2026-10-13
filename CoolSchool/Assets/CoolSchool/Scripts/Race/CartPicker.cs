using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Parent;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Race
{
    /// <summary>
    /// The cart garage at the start of the soapbox race: a panel along the bottom of the screen with six big round tabs down its left
    /// edge (body, paint, add-ons, trail, horn, number) and a grid of picture cards for the open tab, like the dress-up mirror's menu.
    /// Body, paint, trail, horn and number take one choice (a gold ring and a check on the chosen card; paint and trail have a "nothing"
    /// card first); each add-on card switches that add-on on or off. A thing he has won shows its picture, one he has not won yet
    /// shows a black silhouette with a lock. The cart in front of him changes at once (<c>changed</c>), tapping a horn plays it,
    /// tapping a number says it, tapping any card says its name. He taps the green check, or waits: after 20 seconds without a tap
    /// the race starts with what is chosen. No words, he can't read.
    /// The cards come from the reward catalog (items of each cart part, in catalog order), so a new item needs no change here.
    /// The component sits on a holder that stays active (so Update keeps running); everything it shows is under <c>root</c>.
    /// </summary>
    public class CartPicker : MonoBehaviour
    {
        [Tooltip("A child of this object that is switched on while the picker is open.")]
        [SerializeField] GameObject root;
        [Tooltip("Where the pictures and the cards come from.")]
        [SerializeField] RewardCatalog catalog;
        [Tooltip("The six tabs in order: body, paint, add-ons, trail, horn, number. Each has a child \"Icon\" (an Image) and a child \"New\" (a star).")]
        [SerializeField] Button[] tabButtons;
        [Tooltip("For each tab, the id of the item whose picture goes on it (cart.racer_1, a paint pot...). Empty = the tab shows its own digits.")]
        [SerializeField] string[] tabIconItems;
        [Tooltip("Where the cards are made (top left corner = first card).")]
        [SerializeField] RectTransform cardArea;
        [Tooltip("Big green check, bottom right.")]
        [SerializeField] Button goButton;
        [SerializeField] Sprite cardSprite;
        [Tooltip("A rounded outline the size of a card: the gold ring round what is chosen.")]
        [SerializeField] Sprite ringSprite;
        [SerializeField] Sprite circleSprite;
        [SerializeField] Sprite checkSprite;
        [SerializeField] Sprite lockSprite;
        [Tooltip("The picture on the \"nothing\" card (a cross).")]
        [SerializeField] Sprite noneSprite;
        [Tooltip("The little star on things he has not looked at yet.")]
        [SerializeField] Sprite newSprite;
        [Tooltip("For the digits on the number cards.")]
        [SerializeField] Font font;
        [SerializeField] float cardSize = 150f;
        [SerializeField] float cardSpacing = 12f;
        [SerializeField] int cardsPerRow = 6;

        /// <summary>The tabs, in the order of <c>tabButtons</c>.</summary>
        enum Tab
        {
            Body,
            Paint,
            Addon,
            Trail,
            Horn,
            Number
        }

        const int TabCount = 6;
        // The question the announcer asks when a tab is opened for the first time in a race (the body tab has the race's own line).
        static readonly string[] TabPrompts = { "race.tab_body", "race.tab_paint", "race.tab_addon", "race.tab_trail", "race.tab_horn", "race.tab_number" };

        const float NagSeconds = 10f;
        const float AutoStartSeconds = 20f;
        const float LockedLineGapSeconds = 6f;
        const float NameGapSeconds = 1f;
        const float HornSeconds = 1.5f;
        const float IgnoreGoSeconds = 0.5f;
        const float PopInSeconds = 0.35f;
        const float PopInStagger = 0.05f;
        const float PunchSeconds = 0.3f;
        const float WobbleSeconds = 0.45f;
        const float SelectedScale = 1.1f;
        const float SelectedTabScale = 1.15f;
        const float PulseHertz = 0.7f;

        static readonly Color OffColour = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color GoldRing = new Color(1f, 0.8f, 0.1f, 1f);
        static readonly Color TabNormal = new Color(0.85f, 0.88f, 1f, 0.92f);
        static readonly Color TabSelected = new Color(1f, 0.82f, 0.2f, 1f);
        static readonly Color DigitColour = new Color(0.12f, 0.12f, 0.2f, 1f);

        sealed class Card
        {
            public GameObject Go;
            public RectTransform Rect;
            public Image Icon;
            public GameObject Ring;
            public GameObject Check;
            public GameObject LockBadge;
            public RectTransform Twinkle;
            public RewardItem Item;     // null on the "nothing" card and on the number cards
            public bool IsNone;
            public int Number;          // the number cards
            public bool Owned = true;
            public float PunchStart = -100f;
            public float WobbleStart = -100f;
        }

        readonly List<Card> cards = new List<Card>();
        readonly HashSet<int> promptedTabs = new HashSet<int>();
        readonly HashSet<string> newIds = new HashSet<string>();
        CartChoice choice = new CartChoice();
        System.Action<CartChoice> onChanged;
        System.Action onDone;
        bool wired;

        int tab;
        float openedAt;
        float tabOpenedAt;
        float idleSeconds;
        bool nagged;
        float lastLockedLine = -100f;
        float lastNameTime = -100f;

        /// <summary>True while the cards are on the screen.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The catalog the cards come from (the race uses it to find the horn's sound).</summary>
        public RewardCatalog Catalog => catalog;

        /// <summary>What is chosen now.</summary>
        public CartChoice Choice => choice;

        void Awake()
        {
            Wire();
            if (root != null && !IsOpen) root.SetActive(false);
        }

        void Wire()
        {
            if (wired) return;
            wired = true;

            if (tabButtons != null)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    int index = i;
                    if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => OnTabTapped(index));
                }
            }
            if (goButton != null) goButton.onClick.AddListener(TapGo);
        }

        void OnDisable()
        {
            // Never leave the cards up if this object is switched off.
            if (IsOpen) Close();
        }

        /// <summary>
        /// Opens with this choice selected. <paramref name="changed"/> is called with the choice (the same object each time) after every
        /// tap, for the live preview; <paramref name="done"/> when he taps the check or after the auto-start.
        /// </summary>
        public void Open(CartChoice start, System.Action<CartChoice> changed, System.Action done)
        {
            Wire();
            choice = start != null ? start.Clone() : ProgressStore.LoadCartChoice();
            onChanged = changed;
            onDone = done;

            // What he won since last time twinkles; from now on it counts as seen (the race invite stops asking him to come).
            newIds.Clear();
            PlayerProgress progress = ProgressStore.Current;
            if (progress.unseenCartItems != null)
                foreach (string id in progress.unseenCartItems) newIds.Add(id);
            ProgressStore.MarkCartItemsSeen();

            IsOpen = true;
            openedAt = Time.unscaledTime;
            idleSeconds = 0f;
            nagged = false;
            promptedTabs.Clear();
            promptedTabs.Add((int)Tab.Body);   // the race says its own "pick your cart" line
            lastNameTime = -100f;

            if (root != null) root.SetActive(true);
            SetTabPictures();
            SelectTab(0, false);
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

            AnimateCards();
        }

        // ----- Tabs ---------------------------------------------------------------------------------

        void OnTabTapped(int index)
        {
            if (!IsOpen || ParentGate.InputBlocked) return;
            Touched();
            if (index == tab && cards.Count > 0) return;

            SelectTab(index, true);
        }

        void SelectTab(int index, bool byHand)
        {
            tab = Mathf.Clamp(index, 0, TabCount - 1);
            tabOpenedAt = Time.unscaledTime;
            if (byHand) Sfx.Play("ui.pick");

            for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;

                bool selected = i == tab;
                tabButtons[i].transform.localScale = Vector3.one * (selected ? SelectedTabScale : 1f);
                if (tabButtons[i].targetGraphic != null) tabButtons[i].targetGraphic.color = selected ? TabSelected : TabNormal;

                Transform star = tabButtons[i].transform.Find("New");
                if (star != null) star.gameObject.SetActive(TabHasNewItems(i));
            }

            BuildCards();

            // The announcer asks the question of this tab, once per race.
            if (byHand && promptedTabs.Add(tab)) VoicePlayer.Say(TabPrompts[tab], true);
        }

        /// <summary>The tab pictures: the icon of one representative item each (a cart, a paint pot, a balloon...).</summary>
        void SetTabPictures()
        {
            for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;

                Transform icon = tabButtons[i].transform.Find("Icon");
                var image = icon != null ? icon.GetComponent<Image>() : null;
                if (image == null || tabIconItems == null || i >= tabIconItems.Length || string.IsNullOrEmpty(tabIconItems[i])) continue;

                RewardItem item = catalog != null ? catalog.Find(tabIconItems[i]) : null;
                if (item != null && item.icon != null) image.sprite = item.icon;
            }
        }

        bool TabHasNewItems(int index)
        {
            if (newIds.Count == 0 || catalog == null) return false;

            CartPart part = PartOf((Tab)index);
            if (part == CartPart.None) return false;
            foreach (RewardItem item in catalog.ItemsOfPart(part))
                if (newIds.Contains(item.id)) return true;
            return false;
        }

        static CartPart PartOf(Tab tab)
        {
            switch (tab)
            {
                case Tab.Body: return CartPart.Body;
                case Tab.Paint: return CartPart.Paint;
                case Tab.Addon: return CartPart.Addon;
                case Tab.Trail: return CartPart.Trail;
                case Tab.Horn: return CartPart.Horn;
                default: return CartPart.None;
            }
        }

        // ----- Cards --------------------------------------------------------------------------------

        void BuildCards()
        {
            foreach (Card card in cards)
                if (card.Go != null) Destroy(card.Go);
            cards.Clear();
            if (cardArea == null) return;

            var tabKind = (Tab)tab;
            if (tabKind == Tab.Number)
            {
                for (int n = CartChoice.MinNumber; n <= CartChoice.MaxNumber; n++)
                    cards.Add(MakeCard(null, false, n, cards.Count));
            }
            else if (catalog != null)
            {
                // Paint and trail start with a "nothing" card.
                if (tabKind == Tab.Paint || tabKind == Tab.Trail) cards.Add(MakeCard(null, true, 0, cards.Count));
                foreach (RewardItem item in catalog.ItemsOfPart(PartOf(tabKind)))
                    cards.Add(MakeCard(item, false, 0, cards.Count));
            }
            RefreshState();
        }

        Card MakeCard(RewardItem item, bool none, int number, int index)
        {
            PlayerProgress progress = ProgressStore.Current;
            var card = new Card { Item = item, IsNone = none, Number = number, Owned = item == null || item.IsOwnedBy(progress) };

            card.Go = new GameObject(item != null ? "Card_" + item.id : none ? "Card_Nothing" : "Card_" + number, typeof(RectTransform), typeof(Image), typeof(Button));
            card.Go.layer = gameObject.layer;
            card.Rect = (RectTransform)card.Go.transform;
            card.Rect.SetParent(cardArea, false);
            // Anchored to the area's top left corner; the card is positioned by its middle.
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0f, 1f);
            card.Rect.pivot = new Vector2(0.5f, 0.5f);
            card.Rect.sizeDelta = new Vector2(cardSize, cardSize);
            int perRow = Mathf.Max(1, cardsPerRow);
            int column = index % perRow;
            int row = index / perRow;
            // The grid grows down from the area's top left corner; each card is positioned by its middle.
            card.Rect.anchoredPosition = new Vector2(cardSize * 0.5f + column * (cardSize + cardSpacing), -cardSize * 0.5f - row * (cardSize + cardSpacing));

            var background = card.Go.GetComponent<Image>();
            background.sprite = cardSprite;
            background.type = Image.Type.Simple;
            background.color = card.Owned ? new Color(1f, 1f, 1f, 0.93f) : new Color(0.7f, 0.72f, 0.82f, 0.88f);

            var button = card.Go.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => OnCardTapped(card));

            if (number > 0)
            {
                MakeDigit(card.Rect, number);
            }
            else
            {
                RectTransform icon = Child("Icon", card.Rect, cardSize * 0.08f);
                card.Icon = icon.gameObject.AddComponent<Image>();
                card.Icon.raycastTarget = false;
                card.Icon.preserveAspect = true;
                if (none)
                {
                    card.Icon.sprite = noneSprite;
                    card.Icon.color = new Color(1f, 1f, 1f, 0.55f);
                }
                else
                {
                    card.Icon.sprite = item != null && item.icon != null ? item.icon : newSprite;
                    card.Icon.color = card.Owned ? Color.white : Color.black;
                }
            }

            card.Ring = MakeRing(card.Rect);
            card.Check = MakeCheck(card.Rect);
            if (!card.Owned) card.LockBadge = MakeLock(card.Rect);
            if (item != null && card.Owned && newIds.Contains(item.id)) card.Twinkle = MakeTwinkle(card.Rect);
            return card;
        }

        void MakeDigit(RectTransform parent, int number)
        {
            RectTransform rect = Child("Digit", parent, 0f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = number >= 10 ? Mathf.RoundToInt(cardSize * 0.58f) : Mathf.RoundToInt(cardSize * 0.8f);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = DigitColour;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = number.ToString();
        }

        GameObject MakeRing(RectTransform parent)
        {
            RectTransform ring = Child("Ring", parent, -cardSize * 0.04f);
            var image = ring.gameObject.AddComponent<Image>();
            image.sprite = ringSprite != null ? ringSprite : cardSprite;
            image.type = Image.Type.Simple;
            image.color = GoldRing;
            image.raycastTarget = false;
            ring.gameObject.SetActive(false);
            return ring.gameObject;
        }

        GameObject MakeCheck(RectTransform parent)
        {
            var badge = new GameObject("Check", typeof(RectTransform), typeof(Image));
            badge.layer = gameObject.layer;
            var rect = (RectTransform)badge.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cardSize * 0.34f, cardSize * 0.34f);
            rect.anchoredPosition = new Vector2(-cardSize * 0.08f, -cardSize * 0.08f);

            var back = badge.GetComponent<Image>();
            back.sprite = circleSprite != null ? circleSprite : cardSprite;
            back.color = new Color(0.3f, 0.76f, 0.35f, 1f);
            back.raycastTarget = false;

            RectTransform mark = Child("Mark", rect, cardSize * 0.03f);
            var markImage = mark.gameObject.AddComponent<Image>();
            markImage.sprite = checkSprite;
            markImage.color = Color.white;
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
            badge.SetActive(false);
            return badge;
        }

        GameObject MakeLock(RectTransform parent)
        {
            var badge = new GameObject("Lock", typeof(RectTransform), typeof(Image));
            badge.layer = gameObject.layer;
            var rect = (RectTransform)badge.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cardSize * 0.36f, cardSize * 0.36f);
            rect.anchoredPosition = new Vector2(-cardSize * 0.2f, cardSize * 0.2f);

            var back = badge.GetComponent<Image>();
            back.sprite = circleSprite != null ? circleSprite : cardSprite;
            back.color = new Color(0.15f, 0.17f, 0.3f, 1f);
            back.raycastTarget = false;

            RectTransform mark = Child("LockIcon", rect, cardSize * 0.06f);
            var markImage = mark.gameObject.AddComponent<Image>();
            markImage.sprite = lockSprite != null ? lockSprite : circleSprite;
            markImage.color = lockSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
            return badge;
        }

        RectTransform MakeTwinkle(RectTransform parent)
        {
            var star = new GameObject("Twinkle", typeof(RectTransform), typeof(Image));
            star.layer = gameObject.layer;
            var rect = (RectTransform)star.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cardSize * 0.34f, cardSize * 0.34f);
            rect.anchoredPosition = new Vector2(cardSize * 0.12f, -cardSize * 0.12f);

            var image = star.GetComponent<Image>();
            image.sprite = newSprite;
            image.color = new Color(1f, 0.9f, 0.3f, 1f);
            image.raycastTarget = false;
            return rect;
        }

        RectTransform Child(string childName, RectTransform parent, float inset)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        // ----- Taps ---------------------------------------------------------------------------------

        void OnCardTapped(Card card)
        {
            if (!IsOpen || ParentGate.InputBlocked) return;
            Touched();

            if (!card.Owned)
            {
                Locked(card);
                return;
            }

            switch ((Tab)tab)
            {
                case Tab.Body:
                    choice.bodyId = card.Item.id;
                    break;
                case Tab.Paint:
                    choice.paintId = card.IsNone ? "" : card.Item.id;
                    break;
                case Tab.Addon:
                    choice.SetAddon(card.Item.id, !choice.AddonOn(card.Item.id));
                    break;
                case Tab.Trail:
                    choice.trailId = card.IsNone ? "" : card.Item.id;
                    break;
                case Tab.Horn:
                    choice.hornId = card.Item.id;
                    Sfx.PlayShort(card.Item.soundCue, HornSeconds);
                    break;
                default:
                    choice.number = card.Number;
                    VoicePlayer.Say("number." + card.Number, true);
                    break;
            }

            card.PunchStart = Time.unscaledTime;
            Sfx.Play("ui.pick");
            if (card.Item != null) SayName(card.Item);
            Changed();
        }

        /// <summary>The name of the item, but not more than once a second, so quick taps do not cut every line off.</summary>
        void SayName(RewardItem item)
        {
            if (Time.unscaledTime - lastNameTime < NameGapSeconds) return;

            string group = ItemVoice.GroupFor(item);
            if (string.IsNullOrEmpty(group)) return;
            lastNameTime = Time.unscaledTime;
            VoicePlayer.Say(group, true);
        }

        void Locked(Card card)
        {
            card.WobbleStart = Time.unscaledTime;
            Sfx.Play("ui.locked");
            if (Time.unscaledTime - lastLockedLine >= LockedLineGapSeconds)
            {
                lastLockedLine = Time.unscaledTime;
                VoicePlayer.Say("race.cart_locked", true);
            }
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

        void Changed()
        {
            onChanged?.Invoke(choice);
            RefreshState();
        }

        void Finish()
        {
            System.Action done = onDone;
            Close();
            done?.Invoke();
        }

        // ----- Looks --------------------------------------------------------------------------------

        bool IsChosen(Card card)
        {
            switch ((Tab)tab)
            {
                case Tab.Body: return card.Item != null && card.Item.id == choice.bodyId;
                case Tab.Paint: return card.IsNone ? string.IsNullOrEmpty(choice.paintId) : card.Item != null && card.Item.id == choice.paintId;
                case Tab.Addon: return card.Item != null && card.Owned && choice.AddonOn(card.Item.id);
                case Tab.Trail: return card.IsNone ? string.IsNullOrEmpty(choice.trailId) : card.Item != null && card.Item.id == choice.trailId;
                case Tab.Horn:
                    string horn = string.IsNullOrEmpty(choice.hornId) ? ProgressStore.StarterHornId : choice.hornId;
                    return card.Item != null && card.Item.id == horn;
                default: return card.Number == choice.number;
            }
        }

        /// <summary>Ring, check, dimming and twinkle of every card, for what is chosen now.</summary>
        void RefreshState()
        {
            bool toggles = (Tab)tab == Tab.Addon;
            foreach (Card card in cards)
            {
                bool chosen = card.Owned && IsChosen(card);
                if (card.Ring != null) card.Ring.SetActive(chosen);
                if (card.Check != null) card.Check.SetActive(chosen);

                if (card.Icon != null && !card.IsNone)
                {
                    // Locked: the black silhouette. Owned: white; an add-on that is off is dimmed.
                    card.Icon.color = !card.Owned ? Color.black : (toggles && !chosen ? OffColour : Color.white);
                }
            }
        }

        /// <summary>The pop in, the breathing of the chosen card, the punch of a tap, the wobble of a locked one, the twinkle of the new ones.</summary>
        void AnimateCards()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                if (card.Rect == null) continue;

                bool chosen = card.Owned && IsChosen(card);
                float scale = chosen ? SelectedScale : 1f;
                if (chosen) scale *= 1f + 0.03f * Mathf.Sin(now * Mathf.PI * 2f * PulseHertz);

                float popT = Mathf.Clamp01((now - tabOpenedAt - i * PopInStagger) / PopInSeconds);
                scale *= EaseOutBack(popT);

                float punchT = (now - card.PunchStart) / PunchSeconds;
                if (punchT >= 0f && punchT < 1f) scale *= 1f + 0.22f * Mathf.Sin(punchT * Mathf.PI);

                float angle = 0f;
                float wobbleT = (now - card.WobbleStart) / WobbleSeconds;
                if (wobbleT >= 0f && wobbleT < 1f) angle = Mathf.Sin(wobbleT * Mathf.PI * 6f) * 9f * (1f - wobbleT);

                card.Rect.localScale = Vector3.one * scale;
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, angle);

                if (card.Twinkle != null)
                {
                    card.Twinkle.localScale = Vector3.one * (1f + 0.22f * Mathf.Sin(now * 5f));
                    card.Twinkle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * 3f) * 14f);
                }
            }
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
