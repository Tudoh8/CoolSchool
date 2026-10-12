using CoolSchool.Battle;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// Anything the ray gun may aim at: an alien kid that is not a friend yet and not hiding, or a blob of slime.
    /// The Hittable must be on the Opponents team for the gun to find it.
    /// </summary>
    public interface IZapTarget
    {
        /// <summary>False while the target is a friend already, hiding, or switched off.</summary>
        bool CanBeZapped { get; }

        Hittable Hittable { get; }
    }
}
