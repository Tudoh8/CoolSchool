using System.Collections.Generic;
using CoolSchool.Voice;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The names of the eight new cart bodies of the garage (Docs/specs/cart-garage-contract.md, part G2). Each is said by the Narrator (Lily)
    /// when its present opens, when he taps its card in the cart picker and when he walks past it in the pit lane (group item.cart.&lt;name&gt;).
    /// </summary>
    public static partial class VoiceLineSeeder
    {
        static IEnumerable<Seed> BodySeeds()
        {
            yield return new Seed("item.cart.rocket.1", Speaker.Narrator, "De raket!", "The rocket!");
            yield return new Seed("item.cart.firetruck.1", Speaker.Narrator, "De brandweerauto!", "The fire truck!");
            yield return new Seed("item.cart.wagon.1", Speaker.Narrator, "De bolderkar!", "The wagon!");
            yield return new Seed("item.cart.horse.1", Speaker.Narrator, "Het hobbelpaard!", "The rocking horse!");
            yield return new Seed("item.cart.police.1", Speaker.Narrator, "De politieauto!", "The police car!");
            yield return new Seed("item.cart.boat.1", Speaker.Narrator, "De bootkar!", "The boat cart!");
            yield return new Seed("item.cart.trike.1", Speaker.Narrator, "De driewieler!", "The tricycle!");
            yield return new Seed("item.cart.convertible.1", Speaker.Narrator, "De open auto!", "The open car!");
        }
    }
}
