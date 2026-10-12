using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoolSchool.Audio;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Menu CoolSchool/Audio/Auto-Assign Clips From Folder...: pick a folder with an imported sound pack and every
    /// empty cue gets up to four clips whose file names contain one of the cue's keywords. Cues that already
    /// have clips are left alone. A report of assigned and unassigned cues is printed in the console.
    /// </summary>
    public static class SoundAutoAssign
    {
        const int MaxClipsPerCue = 4;
        // Sound effects longer than this are probably music or ambience; music shorter than MinMusicSeconds is a jingle.
        const float MaxEffectSeconds = 12f;
        const float MinMusicSeconds = 20f;

        /// <summary>
        /// Extra file name keywords per cue, found in addition to the words of the cue's last part
        /// (sword_swing gives "sword" and "swing" too). Add more words here when a pack uses other names.
        /// The earlier a keyword, the better the match.
        /// </summary>
        static readonly Dictionary<string, string[]> Synonyms = new Dictionary<string, string[]>
        {
            // UI
            { "ui.tap", new[] { "tap", "click", "tick", "select" } },
            { "ui.button", new[] { "button", "click", "press", "pop", "select" } },
            { "ui.card_right", new[] { "correct", "right", "success", "win", "ding", "chime", "positive" } },
            { "ui.card_wrong", new[] { "wrong", "boing", "bonk", "oops", "negative", "error" } },
            { "ui.popup_open", new[] { "open", "popup", "whoosh", "swoosh", "appear" } },
            { "ui.popup_close", new[] { "close", "pop", "whoosh", "swoosh", "disappear" } },

            // Player
            { "player.step", new[] { "step", "footstep", "foot", "walk", "run", "grass" } },
            { "player.jump", new[] { "jump", "hop", "boing", "spring" } },
            { "player.land", new[] { "land", "thud", "thump", "impact", "drop" } },
            { "player.bump", new[] { "bump", "bonk", "boing", "oof", "hit", "thud" } },

            // Activities
            { "zone.enter", new[] { "enter", "start", "magic", "sparkle", "chime", "ding" } },
            { "zone.invite_chime", new[] { "invite", "chime", "bell", "ding", "notification", "magic" } },
            { "activity.start", new[] { "start", "begin", "whistle", "go" } },
            { "activity.end_cheer", new[] { "cheer", "applause", "clap", "hooray", "yay", "crowd" } },

            // Tag
            { "tag.tagged", new[] { "tag", "tap", "pop", "boing", "slap", "poke" } },
            { "tag.whoosh", new[] { "whoosh", "swoosh", "swish", "wind", "dash" } },

            // Battle
            { "battle.sword_swing", new[] { "swing", "swoosh", "whoosh", "slash", "swish", "sword" } },
            { "battle.hit_confetti", new[] { "pop", "hit", "confetti", "party", "burst", "impact" } },
            { "battle.shield_block", new[] { "shield", "block", "clang", "metal", "parry", "clink" } },
            { "battle.tumble", new[] { "tumble", "fall", "thud", "crash", "topple", "drop" } },
            { "battle.giggle", new[] { "giggle", "laugh", "chuckle", "kid", "hehe" } },
            { "battle.goblin_appear", new[] { "goblin", "appear", "spawn", "pop", "poof", "monster" } },
            { "battle.wave_horn", new[] { "horn", "trumpet", "fanfare", "war", "signal", "brass" } },
            { "dragon.roar", new[] { "roar", "growl", "dragon", "monster", "creature" } },
            { "dragon.fire", new[] { "fire", "flame", "breath", "burn", "whoosh", "fireball" } },
            { "dragon.wing_flap", new[] { "wing", "flap", "flutter", "bird", "wind" } },
            { "dragon.happy", new[] { "happy", "purr", "growl", "roar", "dragon", "cute" } },

            // Imagination
            { "imagination.sparkle", new[] { "sparkle", "twinkle", "magic", "glitter", "shimmer", "chime", "fairy" } },

            // Rewards
            { "reward.star_fly", new[] { "whoosh", "swoosh", "fly", "shoot", "sparkle", "rise" } },
            { "reward.star_land", new[] { "star", "twinkle", "chime", "coin", "ding", "collect", "pickup" } },
            { "reward.jar_full", new[] { "full", "fanfare", "win", "success", "complete", "tada", "level" } },
            { "reward.present_shake", new[] { "shake", "rattle", "present", "gift", "box" } },
            { "reward.present_open", new[] { "open", "present", "gift", "unwrap", "burst", "tada", "pop" } },
            { "reward.item_reveal", new[] { "reveal", "tada", "fanfare", "magic", "win", "ta-da" } },

            // Lesson
            { "lesson.toy_pop", new[] { "pop", "bubble", "bloop", "plop", "appear" } },
            { "lesson.toy_into_basket", new[] { "basket", "drop", "plop", "thud", "toss", "bloop" } },
            { "lesson.chalk", new[] { "chalk", "scribble", "scratch", "write", "draw", "pencil" } },
            { "lesson.bell", new[] { "bell", "school", "ring", "chime", "ding" } },
            { "lesson.trace_progress", new[] { "scratch", "chalk", "scribble", "draw", "pencil", "rub" } },
            { "lesson.letter_pop", new[] { "pop", "ding", "chime", "bubble", "sparkle", "twinkle" } },

            // Aliens
            { "aliens.rocket_land", new[] { "land", "rocket", "spaceship", "ship", "thruster", "descend" } },
            { "aliens.rocket_takeoff", new[] { "takeoff", "launch", "rocket", "blast", "thruster", "liftoff" } },
            { "aliens.zap", new[] { "zap", "laser", "blaster", "ray", "pew", "shoot" } },
            { "aliens.zap_hit", new[] { "zap", "hit", "electric", "spark", "shock", "impact" } },
            { "aliens.slime_throw", new[] { "slime", "throw", "toss", "squish", "whoosh" } },
            { "aliens.slime_splat", new[] { "splat", "slime", "squish", "goo", "splash", "wet" } },
            { "aliens.dance_party", new[] { "dance", "party", "disco", "funky", "groove", "cheer" } },
            { "aliens.jump_pad", new[] { "jump", "pad", "spring", "boing", "bounce", "trampoline" } },
            { "aliens.rescue", new[] { "rescue", "save", "hooray", "success", "cheer", "win" } },

            // Ice cream shop
            { "shop.bell", new[] { "bell", "doorbell", "ring", "ding", "chime", "shop" } },
            { "shop.scoop", new[] { "scoop", "dig", "spoon", "scrape", "squish", "shovel" } },
            { "shop.plop", new[] { "plop", "drop", "blob", "thud", "bloop", "splat" } },
            { "shop.bounce", new[] { "bounce", "boing", "spring", "rubber", "bonk" } },
            { "shop.cone_pop", new[] { "pop", "appear", "bubble", "bloop", "spawn" } },
            { "shop.slurp", new[] { "slurp", "lick", "eat", "munch", "yum", "chew", "bite", "crunch" } },
            { "shop.coin_drop", new[] { "coin", "drop", "clink", "cash", "jingle", "money" } },
            { "shop.coin_tap", new[] { "coin", "collect", "pickup", "ding", "tap", "gold" } },
            { "shop.register", new[] { "register", "kaching", "ka-ching", "cha-ching", "ching", "cash", "till", "money" } },

            // Pirate cove
            { "pirates.waves", new[] { "wave", "sea", "ocean", "water" } },
            { "pirates.wheel", new[] { "wheel", "creak", "ship", "rope", "wood" } },
            { "pirates.cannon", new[] { "cannon", "boom", "blast", "explosion" } },
            { "pirates.balloon_pop", new[] { "pop", "balloon" } },
            { "pirates.bones_rattle", new[] { "bone", "rattle", "skeleton", "clatter" } },
            { "pirates.bones_rebuild", new[] { "bone", "assemble", "build", "clatter", "rattle" } },
            { "pirates.parrot", new[] { "parrot", "bird", "squawk" } },
            { "pirates.map_tick", new[] { "tick", "ding", "check", "chime", "map" } },
            { "pirates.dig", new[] { "dig", "shovel", "sand" } },
            { "pirates.chest_rise", new[] { "chest", "rise", "emerge", "dirt", "sand", "magic" } },
            { "pirates.lock_open", new[] { "unlock", "lock", "click" } },
            { "pirates.coin", new[] { "coin", "gold", "pickup" } },
            { "pirates.party", new[] { "party", "cheer", "pirate", "fanfare", "celebration" } },

            // Soapbox race
            { "race.lane", new[] { "whoosh", "swish", "swipe" } },
            { "race.jump", new[] { "jump", "hop", "boing" } },
            { "race.land", new[] { "land", "thud" } },
            { "race.bonk", new[] { "bonk", "cardboard", "box", "hit", "impact" } },
            { "race.splash", new[] { "splash", "water", "puddle" } },
            { "race.coin", new[] { "coin", "pickup", "collect" } },
            { "race.gate_ding", new[] { "ding", "chime", "correct", "success" } },
            { "race.countdown_beep", new[] { "beep", "countdown", "tick" } },
            { "race.go_horn", new[] { "horn", "start", "air horn" } },
            { "race.ramp_whoosh", new[] { "whoosh", "swoosh", "ramp" } },
            { "race.spinout", new[] { "spin", "skid", "tyre" } },
            { "race.cheer", new[] { "cheer", "crowd", "applause" } },
            { "race.brake", new[] { "brake", "skid", "squeak" } },

            // Music
            { "music.race", new[] { "race", "fast", "upbeat", "chase" } },
            { "music.pirates", new[] { "pirate", "sea", "shanty", "ship", "adventure", "sailing" } },
            { "music.shop", new[] { "shop", "store", "market", "ukulele", "bouncy", "cheerful", "happy" } },
            { "music.playground", new[] { "playground", "happy", "cheerful", "playful", "fun", "bright", "kids" } },
            { "music.knights", new[] { "knight", "medieval", "adventure", "castle", "heroic", "epic" } },
            { "music.aliens", new[] { "alien", "space", "sci-fi", "scifi", "cosmic", "galaxy" } },
            { "music.classroom", new[] { "classroom", "calm", "gentle", "lullaby", "soft", "learning" } },
            { "music.victory", new[] { "victory", "win", "triumph", "fanfare", "celebration", "success" } },
        };

        [MenuItem("CoolSchool/Audio/Auto-Assign Clips From Folder...")]
        static void AutoAssignMenu()
        {
            string absolute = EditorUtility.OpenFolderPanel("Folder with sound clips", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absolute)) return;

            string folder = ToProjectPath(absolute);
            if (folder == null)
            {
                EditorUtility.DisplayDialog("Auto-Assign Clips", "Pick a folder inside the Assets folder of the project.", "OK");
                return;
            }

            SoundLibrary library = SoundLibrarySeeder.CreateOrUpdate();
            Debug.Log(AutoAssign(library, folder));
        }

        /// <summary>Fills empty cues with clips from the folder. Returns the report.</summary>
        public static string AutoAssign(SoundLibrary library, string folder)
        {
            List<AudioClip> clips = LoadClips(folder);

            var assigned = new List<string>();
            var skipped = new List<string>();
            var unassigned = new List<string>();

            foreach (SoundEntry entry in library.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.cue)) continue;

                if (entry.clips != null && entry.clips.Any(c => c != null))
                {
                    skipped.Add(entry.cue);
                    continue;
                }

                List<AudioClip> matches = FindMatches(entry.cue, clips);
                if (matches.Count == 0)
                {
                    unassigned.Add(entry.cue);
                    continue;
                }

                entry.clips = matches;
                assigned.Add($"{entry.cue} ({string.Join(", ", matches.Select(c => c.name))})");
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            report.AppendLine($"CoolSchool: auto-assign from {folder}: {clips.Count} clips found, {assigned.Count} cues filled, {unassigned.Count} still empty, {skipped.Count} already had clips.");
            foreach (string line in assigned) report.AppendLine("  assigned:   " + line);
            foreach (string cue in unassigned) report.AppendLine("  unassigned: " + cue);
            return report.ToString();
        }

        /// <summary>The best clips for a cue: more keyword hits and earlier keywords first, at most four.</summary>
        static List<AudioClip> FindMatches(string cue, List<AudioClip> clips)
        {
            string[] keywords = KeywordsFor(cue);
            bool music = SoundCues.IsMusic(cue);

            var scored = new List<(AudioClip clip, int score)>();
            foreach (AudioClip clip in clips)
            {
                // Music wants long clips, effects short ones.
                if (music && clip.length < MinMusicSeconds) continue;
                if (!music && clip.length > MaxEffectSeconds) continue;

                string name = clip.name.ToLowerInvariant();
                int score = 0;
                for (int i = 0; i < keywords.Length; i++)
                {
                    if (name.Contains(keywords[i])) score += keywords.Length - i;
                }
                if (score > 0) scored.Add((clip, score));
            }

            return scored
                .OrderByDescending(s => s.score)
                .ThenBy(s => s.clip.name, System.StringComparer.OrdinalIgnoreCase)
                .Take(MaxClipsPerCue)
                .Select(s => s.clip)
                .ToList();
        }

        /// <summary>Keywords for a cue: the words of its last part first (sword_swing gives sword and swing), then the synonyms.</summary>
        static string[] KeywordsFor(string cue)
        {
            var keywords = new List<string>();
            string last = cue.Substring(cue.LastIndexOf('.') + 1);
            foreach (string word in last.Split('_'))
            {
                string lower = word.ToLowerInvariant();
                // Short filler words would match half the pack.
                if (lower.Length >= 3 && !keywords.Contains(lower)) keywords.Add(lower);
            }

            if (Synonyms.TryGetValue(cue, out string[] extra))
            {
                foreach (string word in extra)
                    if (!keywords.Contains(word)) keywords.Add(word);
            }
            return keywords.ToArray();
        }

        static List<AudioClip> LoadClips(string folder)
        {
            var clips = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) clips.Add(clip);
            }
            return clips;
        }

        /// <summary>"C:/.../Project/Assets/Foo" becomes "Assets/Foo". Null when the folder is outside Assets.</summary>
        static string ToProjectPath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            string assets = Application.dataPath.Replace('\\', '/');
            if (absolute == assets) return "Assets";
            if (!absolute.StartsWith(assets + "/")) return null;
            return "Assets" + absolute.Substring(assets.Length);
        }
    }
}
