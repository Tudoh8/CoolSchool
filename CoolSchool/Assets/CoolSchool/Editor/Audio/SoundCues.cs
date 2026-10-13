namespace CoolSchool.EditorTools
{
    /// <summary>Every sound cue the game calls. The seeder puts all of them in the sound library.</summary>
    public static class SoundCues
    {
        public static readonly string[] All =
        {
            // UI
            "ui.tap", "ui.button", "ui.card_right", "ui.card_wrong", "ui.popup_open", "ui.popup_close",
            "ui.pick", "ui.locked", "wardrobe.equip", "toyroom.sparkle",
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
            "lesson.toy_pop", "lesson.toy_into_basket", "lesson.chalk", "lesson.bell", "lesson.trace_progress", "lesson.letter_pop",
            // Aliens
            "aliens.rocket_land", "aliens.rocket_takeoff", "aliens.zap", "aliens.zap_hit", "aliens.slime_throw",
            "aliens.slime_splat", "aliens.dance_party", "aliens.jump_pad", "aliens.rescue",
            "aliens.spawn", "aliens.giggle", "aliens.shrink", "aliens.peek", "aliens.robot_beep", "aliens.bubble_pop", "aliens.slime_pop",
            // Ice cream shop
            "shop.bell", "shop.scoop", "shop.plop", "shop.bounce", "shop.cone_pop", "shop.slurp",
            "shop.coin_drop", "shop.coin_tap", "shop.register",
            // Pirate cove
            "pirates.waves", "pirates.wheel", "pirates.cannon", "pirates.balloon_pop", "pirates.bones_rattle", "pirates.bones_rebuild",
            "pirates.parrot", "pirates.map_tick", "pirates.dig", "pirates.chest_rise", "pirates.lock_open", "pirates.coin", "pirates.party",
            // Castle gate (the portcullis rising and landing)
            "castle.gate_rise", "castle.gate_drop",
            // Soapbox race
            "race.lane", "race.jump", "race.land", "race.bonk", "race.splash", "race.coin", "race.gate_ding", "race.countdown_beep",
            "race.go_horn", "race.ramp_whoosh", "race.spinout", "race.cheer", "race.brake",
            // Hide and seek in the zoo park (the animal calls are zoo.call.<id>, one per ZooAnimalKind)
            "zoo.giggle", "zoo.patter", "zoo.found_pop", "zoo.peek", "zoo.shake", "zoo.surprise_pop", "zoo.gate", "zoo.pen_hop",
            "zoo.hide_in", "zoo.party",
            "zoo.call.elephant", "zoo.call.giraffe", "zoo.call.monkey", "zoo.call.penguin", "zoo.call.hippo",
            "zoo.call.lion", "zoo.call.zebra", "zoo.call.crocodile", "zoo.call.flamingo", "zoo.call.tiger",
            // Music
            "music.playground", "music.knights", "music.aliens", "music.classroom", "music.victory", "music.shop", "music.pirates", "music.race", "music.zoo",
            // Ambience (a quiet looping bed under the music of each area)
            "ambience.playground", "ambience.knights", "ambience.aliens", "ambience.classroom", "ambience.shop", "ambience.pirates", "ambience.race", "ambience.zoo",
        };

        public static bool IsMusic(string cue) => cue.StartsWith("music.");

        public static bool IsAmbience(string cue) => cue.StartsWith("ambience.");
    }
}
