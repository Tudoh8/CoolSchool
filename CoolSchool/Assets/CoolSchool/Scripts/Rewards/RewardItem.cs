using System;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>One thing that can be won from a present, with the model shown in the 3D reveal.</summary>
    [Serializable]
    public class RewardItem
    {
        public string id;
        public RewardKind kind;
        [Tooltip("The model shown spinning when the present opens.")]
        public GameObject prefab;
        [Tooltip("Extra rotation of the model in the reveal, to make it stand nicely.")]
        public Vector3 revealEuler;
        [Tooltip("Makes the model bigger or smaller in the reveal.")]
        public float revealScale = 1f;
        [Tooltip("Owned from the start and never given in a present (the default sword, shield and cart).")]
        public bool starter;
        [Tooltip("For a gold variant: the id of the item it is a gold copy of. Same model, painted gold at run time.")]
        public string goldOf = "";
        [Tooltip("A picture of the item, rendered by the editor (RewardIconRenderer). Locked items show it tinted black.")]
        public Sprite icon;

        [Header("Cart items only")]
        [Tooltip("Which part of the soapbox cart this is (body, paint, add-on, trail or horn). None for everything that is not a cart item.")]
        public CartPart cartPart;
        [Tooltip("For a paint: the colour of the paint pot and of the painted body.")]
        public Color paintColor = Color.white;
        [Tooltip("For a horn: the sound cue it plays (the honk button, the picker and the present).")]
        public string soundCue = "";

        /// <summary>True for a gold copy of another item.</summary>
        public bool IsGold => !string.IsNullOrEmpty(goldOf);

        /// <summary>The slot this item is worn in (None for cart parts and costumes).</summary>
        public EquipSlot Slot => EquipSlots.SlotOf(kind);

        /// <summary>Starters are always owned; everything else once it has been won.</summary>
        public bool IsOwnedBy(PlayerProgress progress) => starter || (progress != null && progress.IsUnlocked(id));

        /// <summary>True for a cart item of this part (a body, a paint, an add-on, a trail or a horn).</summary>
        public bool IsCartPart(CartPart part) => kind == RewardKind.Cart && cartPart == part;
    }
}
