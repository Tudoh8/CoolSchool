namespace CoolSchool.EditorTools
{
    /// <summary>Every sound cue the game calls. The seeder puts all of them in the sound library.</summary>
    public static class SoundCues
    {
        public static readonly string[] All =
        {
            // UI
            "ui.tap", "ui.button", "ui.card_right", "ui.card_wrong", "ui.popup_open", "ui.popup_close",
            // Player
            "player.step", "player.jump", "player.land", "player.bump",
            // Activities
            "zone.enter", "zone.invite_chime", "activity.start", "activity.end_cheer",
            // Tag
            "tag.tagged", "tag.whoosh",
            // Battle
            "battle.sword_swing", "battle.hit_confetti", "battle.shield_block", "battle.tumble", "battle.giggle",
            "battle.goblin_appear", "battle.wave_horn", "dragon.roar", "dragon.fire", "dragon.wing_flap", "dragon.happy",
            // Imagination
            "imagination.sparkle",
            // Rewards
            "reward.star_fly", "reward.star_land", "reward.jar_full", "reward.present_shake", "reward.present_open", "reward.item_reveal",
            // Lesson
            "lesson.toy_pop", "lesson.toy_into_basket", "lesson.chalk", "lesson.bell",
            // Aliens
            "aliens.rocket_land", "aliens.rocket_takeoff", "aliens.zap", "aliens.zap_hit", "aliens.slime_throw",
            "aliens.slime_splat", "aliens.dance_party", "aliens.jump_pad", "aliens.rescue",
            // Music
            "music.playground", "music.knights", "music.aliens", "music.classroom", "music.victory",
        };

        public static bool IsMusic(string cue) => cue.StartsWith("music.");
    }
}
