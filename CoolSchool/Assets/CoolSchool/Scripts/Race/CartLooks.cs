using System.Collections.Generic;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// What a soapbox cart looks like: which of the three bodies is shown, and the unlocked extras (flag, stickers,
    /// gold paint). The player's cart takes its looks from the presents he has won; the rivals just get a body.
    /// </summary>
    public class CartLooks : MonoBehaviour
    {
        public const string FlagId = "cart.flag";
        public const string StickersId = "cart.stickers";
        public const string GoldId = "cart.gold";
        public const string Racer2Id = "cart.racer_2";
        public const string Racer3Id = "cart.racer_3";

        [Tooltip("Racer 01, 02 and 03 (children of the cart's model). Only one is shown.")]
        [SerializeField] GameObject[] bodies;
        [Tooltip("Inactive by default.")]
        [SerializeField] GameObject flag;
        [Tooltip("Inactive by default.")]
        [SerializeField] GameObject stickers;
        [Tooltip("Replaces every body renderer's materials while gold is on.")]
        [SerializeField] Material goldMaterial;

        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        bool cached;
        bool gold;

        /// <summary>The body that is shown now (0..2).</summary>
        public int BodyIndex { get; private set; }

        void Awake()
        {
            CacheOriginals();
        }

        /// <summary>Shows body 0, 1 or 2 and hides the others. Out of range values are clamped.</summary>
        public void SetBody(int index)
        {
            if (bodies == null || bodies.Length == 0) return;

            BodyIndex = Mathf.Clamp(index, 0, bodies.Length - 1);
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] != null) bodies[i].SetActive(i == BodyIndex);

            // The shown body gets the gold paint too.
            ApplyGold();
        }

        public void SetExtras(bool showFlag, bool showStickers, bool showGold)
        {
            if (flag != null) flag.SetActive(showFlag);
            if (stickers != null) stickers.SetActive(showStickers);

            gold = showGold;
            ApplyGold();
        }

        /// <summary>cart.racer_3 gives body 2, otherwise cart.racer_2 gives body 1, otherwise body 0.</summary>
        public static int PlayerBodyIndex(PlayerProgress progress)
        {
            if (progress == null) return 0;
            if (progress.IsUnlocked(Racer3Id)) return 2;
            if (progress.IsUnlocked(Racer2Id)) return 1;
            return 0;
        }

        /// <summary>The body and the extras from the unlocked cart.* presents.</summary>
        public void ApplyPlayerLooks(PlayerProgress progress)
        {
            SetBody(PlayerBodyIndex(progress));
            SetExtras(
                progress != null && progress.IsUnlocked(FlagId),
                progress != null && progress.IsUnlocked(StickersId),
                progress != null && progress.IsUnlocked(GoldId));
        }

        void CacheOriginals()
        {
            if (cached) return;
            cached = true;

            if (bodies == null) return;
            foreach (GameObject body in bodies)
            {
                if (body == null) continue;
                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                    originalMaterials[renderer] = renderer.sharedMaterials;
            }
        }

        void ApplyGold()
        {
            CacheOriginals();

            foreach (KeyValuePair<Renderer, Material[]> entry in originalMaterials)
            {
                Renderer renderer = entry.Key;
                if (renderer == null) continue;

                if (gold && goldMaterial != null)
                {
                    var painted = new Material[entry.Value.Length];
                    for (int i = 0; i < painted.Length; i++) painted[i] = goldMaterial;
                    renderer.sharedMaterials = painted;
                }
                else
                {
                    renderer.sharedMaterials = entry.Value;
                }
            }
        }
    }
}
