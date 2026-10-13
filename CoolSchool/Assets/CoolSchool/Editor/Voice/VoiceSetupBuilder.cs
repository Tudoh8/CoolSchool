using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.Tag;
using CoolSchool.Voice;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Adds the scene's voice: a "Voice" object with the player, the cue bridge and the battle voice,
    /// and wires the games' events to spoken lines. Called by the playground builder.
    /// </summary>
    public static class VoiceSetupBuilder
    {
        /// <summary>Builds the Voice object. Games left null are looked up in the scene.</summary>
        public static GameObject Setup(TagGame tagGame = null, KnightsGame knightsGame = null)
        {
            if (tagGame == null) tagGame = Object.FindFirstObjectByType<TagGame>();
            if (knightsGame == null) knightsGame = Object.FindFirstObjectByType<KnightsGame>();

            LineTable table = VoiceLineSeeder.CreateOrUpdate();
            VoiceLineExporter.LinkClips(table);

            var voice = new GameObject("Voice");

            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var player = voice.AddComponent<VoicePlayer>();
            PlaygroundPrototypeBuilder.SetObject(player, "table", table);
            PlaygroundPrototypeBuilder.SetObject(player, "source", source);
            PlaygroundPrototypeBuilder.SetBool(player, "showDebugSubtitles", true);

            var cue = voice.AddComponent<VoiceCue>();
            voice.AddComponent<SayOnStart>();

            var battleVoice = voice.AddComponent<BattleVoice>();
            PlaygroundPrototypeBuilder.SetObject(battleVoice, "game", knightsGame);

            WireTagGame(tagGame, cue);
            WireKnightsGame(knightsGame, cue);
            BuildInviter(tagGame, knightsGame);
            return voice;
        }

        /// <summary>
        /// An "ActivityInviter" that leads the kid to the tag zone and the castle zone when nothing is going on.
        /// LessonSetupBuilder runs after this and puts the classroom zone in front of them, so the classroom is invited first.
        /// </summary>
        static void BuildInviter(TagGame tagGame, KnightsGame knightsGame)
        {
            ActivityZone tagZone = null;
            ActivityZone castleZone = null;
            foreach (ActivityZone zone in Object.FindObjectsByType<ActivityZone>(FindObjectsSortMode.None))
            {
                if (tagGame != null && zone.Activity == tagGame) tagZone = zone;
                if (knightsGame != null && zone.Activity == knightsGame) castleZone = zone;
            }

            if (tagZone == null || castleZone == null)
                Debug.LogWarning("CoolSchool: a tag or castle zone is missing, so the activity inviter will skip it.");

            var inviter = new GameObject("ActivityInviter").AddComponent<ActivityInviter>();
            PlaygroundPrototypeBuilder.SetObjectArray(inviter, "zones", new[] { tagZone, castleZone });
            PlaygroundPrototypeBuilder.SetStringArray(inviter, "inviteGroups", new[] { "invite.tag", "invite.knights" });
        }

        static void WireTagGame(TagGame game, VoiceCue cue)
        {
            if (game == null)
            {
                Debug.LogWarning("CoolSchool: no TagGame found, tag voice lines are not wired.");
                return;
            }

            Say(game.onBegan, cue, "tag.start");
            Say(game.onPlayerTaggedSomeone, cue, "tag.tagged_someone");
            Say(game.onPlayerBecameIt, cue, "tag.became_it");
            Say(game.onRoundFinished, cue, "tag.round_finished");
            EditorUtility.SetDirty(game);
        }

        static void WireKnightsGame(KnightsGame game, VoiceCue cue)
        {
            if (game == null)
            {
                Debug.LogWarning("CoolSchool: no KnightsGame found, knights voice lines are not wired.");
                return;
            }

            // The castle adventure says its own start, dragon and victory lines in the flow (castle.* lines), so only the
            // wave horn line is still wired to an event.
            UnityEventTools.AddPersistentListener(game.onWaveStarted, cue.SayWave);
            EditorUtility.SetDirty(game);
        }

        /// <summary>Makes the event call VoiceCue.Say with the line group, saved in the scene.</summary>
        static void Say(UnityEvent unityEvent, VoiceCue cue, string group)
        {
            UnityEventTools.AddStringPersistentListener(unityEvent, cue.Say, group);
        }
    }
}
