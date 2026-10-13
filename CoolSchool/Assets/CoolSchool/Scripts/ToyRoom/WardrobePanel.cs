using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.ToyRoom
{
    /// <summary>
    /// The dress-up mirror's picture menu, on the touch canvas (the right side of the screen). Four big tabs down the left edge
    /// (sword, shield, hat, buddy) and a grid of item cards for the chosen tab. An owned card shows the item's picture, a locked
    /// one shows it as a black silhouette. What he wears now has a gold ring and a check badge; things he has not looked at yet
    /// twinkle. The hat and buddy tabs start with a "nothing" card. No words anywhere: he cannot read.
    /// The panel only shows things and reports taps (ItemChosen, LockedChosen, Done); the wardrobe does the rest.
    /// The cards are made at run time from the catalog; the builder makes the window, the tabs and the Done button.
    /// </summary>
    public class WardrobePanel : MonoBehaviour
    {
        static readonly EquipSlot[] TabSlots = { EquipSlot.Sword, EquipSlot.Shield, EquipSlot.Hat, EquipSlot.Buddy };

        [SerializeField] RewardCatalog catalog;
        [Tooltip("The window; switched on by Show and off by Hide. This component sits on an always-active object.")]
        [SerializeField] RectTransform window;
        [Tooltip("The four tabs: sword, shield, hat, buddy. Their Button's target graphic is the round back of the tab.")]
        [SerializeField] Button[] tabButtons;
        [Tooltip("Where the cards are made (top left corner = first card).")]
        [SerializeField] RectTransform cardArea;
        [SerializeField] Button doneButton;
        [SerializeField] Sprite cardSprite;
        [Tooltip("A rounded outline the size of a card: the gold ring round what he wears.")]
        [SerializeField] Sprite ringSprite;
        [Tooltip("A filled circle: the back of the check badge.")]
        [SerializeField] Sprite circleSprite;
        [SerializeField] Sprite checkSprite;
        [SerializeField] Sprite emptySprite;
        [SerializeField] Sprite twinkleSprite;
        [SerializeField] float cardSize = 124f;
        [SerializeField] float cardSpacing = 10f;
        [SerializeField] int cardsPerRow = 6;

        static readonly Color GoldRing = new Color(1f, 0.8f, 0.15f, 1f);
        static readonly Color TabNormal = new Color(0.85f, 0.88f, 1f, 0.9f);
        static readonly Color TabSelected = new Color(1f, 0.82f, 0.2f, 1f);
        const float SelectedTabScale = 1.15f;

        sealed class Card
        {
            public GameObject Go;
            public RectTransform Rect;
            public Button Button;
            public Image Background;
            public Image Icon;
            public GameObject Ring;
            public GameObject Check;
            public RectTransform Twinkle;
            public RewardItem Item;      // null: the "nothing" card
            public bool Owned;
            public bool Unseen;
            public Coroutine Anim;
        }

        readonly List<Card> cards = new List<Card>();
        int selectedTab;
        bool wired;

        /// <summary>An owned card was tapped. The item is null for the "nothing" card.</summary>
        public event Action<EquipSlot, RewardItem> ItemChosen;

        /// <summary>A locked card was tapped.</summary>
        public event Action<RewardItem> LockedChosen;

        /// <summary>The Done button was tapped.</summary>
        public event Action Done;

        public bool IsShown => window != null && window.gameObject.activeSelf;

        public EquipSlot CurrentSlot => TabSlots[Mathf.Clamp(selectedTab, 0, TabSlots.Length - 1)];

        void Awake()
        {
            Wire();
            if (window != null) window.gameObject.SetActive(false);
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
            if (doneButton != null) doneButton.onClick.AddListener(() => Done?.Invoke());
        }

        void OnDisable()
        {
            if (window != null) window.gameObject.SetActive(false);
        }

        /// <summary>Opens the window on the sword tab.</summary>
        public void Show()
        {
            Wire();
            if (window == null) return;

            window.gameObject.SetActive(true);
            SelectTab(0, false);
            StopAllCoroutines();
            StartCoroutine(PopIn());
        }

        /// <summary>Closes the window. Safe to call at any time.</summary>
        public void Hide()
        {
            StopAllCoroutines();
            if (window != null) window.gameObject.SetActive(false);
        }

        void OnTabTapped(int index)
        {
            if (index == selectedTab && cards.Count > 0) return;
            SelectTab(index, true);
        }

        void SelectTab(int index, bool sound)
        {
            selectedTab = Mathf.Clamp(index, 0, TabSlots.Length - 1);
            if (sound) Sfx.Play("ui.pick");

            for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;

                bool selected = i == selectedTab;
                tabButtons[i].transform.localScale = Vector3.one * (selected ? SelectedTabScale : 1f);
                if (tabButtons[i].targetGraphic != null) tabButtons[i].targetGraphic.color = selected ? TabSelected : TabNormal;
            }
            BuildCards();
        }

        // ----- Cards -------------------------------------------------------------------------------

        void BuildCards()
        {
            foreach (Card card in cards)
                if (card.Go != null) Destroy(card.Go);
            cards.Clear();
            if (cardArea == null || catalog == null) return;

            EquipSlot slot = CurrentSlot;
            PlayerProgress progress = ProgressStore.Current;
            int index = 0;

            if (slot == EquipSlot.Hat || slot == EquipSlot.Buddy)
            {
                cards.Add(MakeCard(null, slot, true, false, index++));
            }

            foreach (RewardItem item in catalog.ItemsFor(slot))
            {
                bool owned = item.IsOwnedBy(progress);
                bool unseen = owned && progress.unseenItems != null && progress.unseenItems.Contains(item.id);
                cards.Add(MakeCard(item, slot, owned, unseen, index++));
            }
            RefreshWorn();
        }

        Card MakeCard(RewardItem item, EquipSlot slot, bool owned, bool unseen, int index)
        {
            var card = new Card { Item = item, Owned = owned, Unseen = unseen };

            card.Go = new GameObject(item != null ? "Card_" + item.id : "Card_Nothing", typeof(RectTransform), typeof(Image), typeof(Button));
            card.Go.layer = gameObject.layer;
            card.Rect = (RectTransform)card.Go.transform;
            card.Rect.SetParent(cardArea, false);
            card.Rect.anchorMin = card.Rect.anchorMax = card.Rect.pivot = new Vector2(0f, 1f);
            card.Rect.sizeDelta = new Vector2(cardSize, cardSize);
            int column = index % Mathf.Max(1, cardsPerRow);
            int row = index / Mathf.Max(1, cardsPerRow);
            card.Rect.anchoredPosition = new Vector2(column * (cardSize + cardSpacing), -row * (cardSize + cardSpacing));

            card.Background = card.Go.GetComponent<Image>();
            card.Background.sprite = cardSprite;
            card.Background.type = Image.Type.Simple;
            card.Background.color = owned ? new Color(1f, 1f, 1f, 0.92f) : new Color(0.7f, 0.72f, 0.82f, 0.85f);

            card.Button = card.Go.GetComponent<Button>();
            card.Button.transition = Selectable.Transition.None;
            card.Button.onClick.AddListener(() => OnCardTapped(card, slot));

            // The picture: white (as it is) when owned, black (only its shape) when locked. "Nothing" is a faded cross.
            RectTransform icon = Child("Icon", card.Rect, 12f);
            card.Icon = icon.gameObject.AddComponent<Image>();
            card.Icon.raycastTarget = false;
            card.Icon.preserveAspect = true;
            if (item == null)
            {
                card.Icon.sprite = emptySprite;
                card.Icon.color = new Color(1f, 1f, 1f, 0.55f);
            }
            else
            {
                card.Icon.sprite = item.icon != null ? item.icon : twinkleSprite;
                card.Icon.color = owned ? Color.white : Color.black;
            }

            card.Ring = MakeRing(card.Rect);
            card.Check = MakeCheck(card.Rect);
            if (unseen) card.Twinkle = MakeTwinkle(card.Rect);
            return card;
        }

        GameObject MakeRing(RectTransform parent)
        {
            RectTransform ring = Child("Ring", parent, -4f);
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

            RectTransform mark = Child("Mark", rect, 4f);
            var markImage = mark.gameObject.AddComponent<Image>();
            markImage.sprite = checkSprite;
            markImage.color = Color.white;
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
            badge.SetActive(false);
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
            image.sprite = twinkleSprite;
            image.color = new Color(1f, 0.9f, 0.3f, 1f);
            image.raycastTarget = false;
            return rect;
        }

        RectTransform Child(string name, RectTransform parent, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        void OnCardTapped(Card card, EquipSlot slot)
        {
            if (card.Owned)
            {
                Punch(card);
                ItemChosen?.Invoke(slot, card.Item);
            }
            else
            {
                Wobble(card);
                LockedChosen?.Invoke(card.Item);
            }
        }

        /// <summary>Puts the gold ring and the check on what he wears now, and stops the twinkle on what he has looked at.</summary>
        public void RefreshWorn()
        {
            EquipSlot slot = CurrentSlot;
            string worn = ProgressStore.EquippedId(slot);
            PlayerProgress progress = ProgressStore.Current;

            foreach (Card card in cards)
            {
                bool isWorn = card.Item == null ? string.IsNullOrEmpty(worn) : card.Item.id == worn;
                if (card.Ring != null) card.Ring.SetActive(isWorn);
                if (card.Check != null) card.Check.SetActive(isWorn);

                bool unseen = card.Item != null && card.Owned && progress.unseenItems != null && progress.unseenItems.Contains(card.Item.id);
                if (!unseen && card.Twinkle != null)
                {
                    Destroy(card.Twinkle.gameObject);
                    card.Twinkle = null;
                }
            }
        }

        void Update()
        {
            if (!IsShown) return;

            // The twinkle on new things: a slow pulse and a little turn.
            float pulse = 1f + 0.22f * Mathf.Sin(Time.unscaledTime * 5f);
            foreach (Card card in cards)
            {
                if (card.Twinkle == null) continue;

                card.Twinkle.localScale = Vector3.one * pulse;
                card.Twinkle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 3f) * 14f);
            }
        }

        // ----- Little animations -------------------------------------------------------------------

        void Wobble(Card card)
        {
            if (card.Anim != null) StopCoroutine(card.Anim);
            card.Rect.localScale = Vector3.one;
            card.Anim = StartCoroutine(WobbleRoutine(card));
        }

        IEnumerator WobbleRoutine(Card card)
        {
            const float seconds = 0.4f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (card.Rect == null) yield break;

                float fade = 1f - t / seconds;
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 45f) * 9f * fade);
                yield return null;
            }
            if (card.Rect != null) card.Rect.localRotation = Quaternion.identity;
            card.Anim = null;
        }

        void Punch(Card card)
        {
            if (card.Anim != null) StopCoroutine(card.Anim);
            card.Rect.localRotation = Quaternion.identity;
            card.Anim = StartCoroutine(PunchRoutine(card));
        }

        IEnumerator PunchRoutine(Card card)
        {
            const float seconds = 0.3f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (card.Rect == null) yield break;

                card.Rect.localScale = Vector3.one * (1f + 0.22f * Mathf.Sin(t / seconds * Mathf.PI));
                yield return null;
            }
            if (card.Rect != null) card.Rect.localScale = Vector3.one;
            card.Anim = null;
        }

        IEnumerator PopIn()
        {
            const float seconds = 0.3f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / seconds);
                window.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            window.localScale = Vector3.one;
        }
    }
}
