namespace CoolSchool.Rewards
{
    /// <summary>The places on the kid where a won item can be worn or carried along.</summary>
    public enum EquipSlot
    {
        None,
        Sword,
        Shield,
        Hat,
        Buddy
    }

    /// <summary>The starter item ids and the mapping from item kind to slot.</summary>
    public static class EquipSlots
    {
        /// <summary>The foam sword: owned from the start, and the default sword.</summary>
        public const string StarterSwordId = "sword.foam";
        /// <summary>The basic shield: owned from the start, and the default shield.</summary>
        public const string StarterShieldId = "shield.basic";
        /// <summary>Soapbox cart one: owned from the start, and the default cart.</summary>
        public const string StarterCartId = "cart.racer_1";
        /// <summary>Lorre the parrot, who sits on his shoulder.</summary>
        public const string ParrotId = "plush.parrot";

        /// <summary>Sword to Sword, Shield to Shield, Hat to Hat, PlushToy to Buddy, anything else to None.</summary>
        public static EquipSlot SlotOf(RewardKind kind)
        {
            switch (kind)
            {
                case RewardKind.Sword: return EquipSlot.Sword;
                case RewardKind.Shield: return EquipSlot.Shield;
                case RewardKind.Hat: return EquipSlot.Hat;
                case RewardKind.PlushToy: return EquipSlot.Buddy;
                default: return EquipSlot.None;
            }
        }
    }
}
