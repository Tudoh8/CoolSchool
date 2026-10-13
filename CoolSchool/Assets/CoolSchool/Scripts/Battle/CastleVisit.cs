namespace CoolSchool.Battle
{
    /// <summary>The story of one castle visit: what the queen asks the knight to protect or do. Rotates between visits.</summary>
    public enum CastleMission
    {
        /// <summary>Guard the party cake.</summary>
        Cake,
        /// <summary>Protect the golden crown.</summary>
        Crown,
        /// <summary>Protect the dragon egg (the dragon turns out to be its sad mother).</summary>
        Egg,
        /// <summary>Rescue a knight who is stuck on top of the tower.</summary>
        Knight
    }

    /// <summary>The counting moment of a visit: count the goblins on the wall, or count the cookies the dragon wants.</summary>
    public enum CastleNumberMoment
    {
        Goblins,
        Cookies
    }

    /// <summary>The letter moment at the end of a visit: his name on the flag, naming the baby dragon, or none.</summary>
    public enum CastleLetterMoment
    {
        None,
        NameFlag,
        NameBabyDragon
    }

    /// <summary>The voice-line keys that belong to a mission, so the flow does not spell them out in many places.</summary>
    public static class CastleVisit
    {
        /// <summary>"cake", "crown", "egg" or "knight": the part after "castle.mission." and "castle.thief_grab.".</summary>
        public static string Key(CastleMission mission)
        {
            switch (mission)
            {
                case CastleMission.Crown: return "crown";
                case CastleMission.Egg: return "egg";
                case CastleMission.Knight: return "knight";
                default: return "cake";
            }
        }

        /// <summary>True for the missions in which a thief goblin runs off with something (all but the knight).</summary>
        public static bool HasThief(CastleMission mission)
        {
            return mission != CastleMission.Knight;
        }
    }
}
