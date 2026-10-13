namespace CoolSchool.Rewards
{
    /// <summary>
    /// Which part of the soapbox cart a <see cref="RewardKind.Cart"/> item is. The cart picker has one tab for each part
    /// (the number plate has no items: he picks his number for free). None is every item that is not a cart item.
    /// </summary>
    public enum CartPart
    {
        None,
        /// <summary>One of the cart bodies; exactly one is shown.</summary>
        Body,
        /// <summary>A paint colour; at most one is on.</summary>
        Paint,
        /// <summary>Something that sits on the cart (flag, stickers, eyes...); each is on or off.</summary>
        Addon,
        /// <summary>What the cart leaves behind it while it drives; at most one is on.</summary>
        Trail,
        /// <summary>The sound of the honk button; exactly one is chosen (the starter bell until he picks another).</summary>
        Horn
    }
}
