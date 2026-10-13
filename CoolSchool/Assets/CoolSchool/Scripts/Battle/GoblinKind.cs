namespace CoolSchool.Battle
{
    /// <summary>
    /// Which sort of goblin a BattleOpponentAI is in the castle adventure. The builder sets it per pool member.
    /// Normal is the classic club goblin, Shield holds a shield up until it swings (then a block makes it dizzy),
    /// Big is the troll that shrinks with every bump, Tiny hides in a barrel and never attacks.
    /// </summary>
    public enum GoblinKind
    {
        Normal,
        Shield,
        Big,
        Tiny
    }
}
