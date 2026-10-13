namespace CoolSchool.Zoo
{
    /// <summary>How a hiding spot hides: behind it, inside it, up in it, or under it.</summary>
    public enum HidingSpotKind
    {
        /// <summary>The animal stands north of the cover (the camera's far side), so its top shows over the cover.</summary>
        Cover,
        /// <summary>The animal sinks into it (bin, box, tent, pond...), so only its top shows.</summary>
        Container,
        /// <summary>Up in a tree crown.</summary>
        Perch,
        /// <summary>Under a table.</summary>
        Under,
    }
}
