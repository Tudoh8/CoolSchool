using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>Paints a spawned item model gold. Used for the gold variants of the presents.</summary>
    public static class GoldPaint
    {
        /// <summary>A softer glow than the lesson's golden-sword power-up, so the power-up still stands out.</summary>
        public static readonly Color SoftGlow = new Color(0.45f, 0.34f, 0.07f, 1f);

        /// <summary>Paints every renderer of this instance gold (the same look as the golden-sword power-up, but a softer glow).</summary>
        public static void Apply(GameObject instance)
        {
            if (instance == null) return;
            WeaponHolder.ApplyGlow(instance, SoftGlow);
        }
    }
}
