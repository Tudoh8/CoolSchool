using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CoolSchool.Voice;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Writes the spoken lines to text files for text-to-speech (DeepVoice) and links the generated
    /// audio clips back to the lines. The clip names follow one rule: "nl_" or "en_" plus the key with dots as underscores.
    /// </summary>
    public static class VoiceLineExporter
    {
        const string DutchPrefix = "nl_";
        const string EnglishPrefix = "en_";
        public const string GeneratedClipsFolder = "Assets/CoolSchool/Voice/Clips";

        /// <summary>The file name a generated clip should have, for example nl_tag_start_1.</summary>
        public static string ClipName(string prefix, string key)
        {
            return prefix + key.Replace('.', '_');
        }

        [MenuItem("CoolSchool/Voice/Export Lines for DeepVoice")]
        static void ExportMenu()
        {
            LineTable table = VoiceLineSeeder.LoadOrCreate();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "voice"));
            Directory.CreateDirectory(folder);

            string dutchPath = Path.Combine(folder, "lines_nl.txt");
            string englishPath = Path.Combine(folder, "lines_en.txt");
            File.WriteAllText(dutchPath, BuildText(table, DutchPrefix, line => line.dutch), new UTF8Encoding(false));
            File.WriteAllText(englishPath, BuildText(table, EnglishPrefix, line => line.english), new UTF8Encoding(false));

            Debug.Log($"CoolSchool: exported voice lines to {dutchPath} and {englishPath}");
            EditorUtility.RevealInFinder(dutchPath);
        }

        static string BuildText(LineTable table, string prefix, Func<VoiceLine, string> textOf)
        {
            var text = new StringBuilder();
            foreach (Speaker speaker in Enum.GetValues(typeof(Speaker)))
            {
                List<VoiceLine> lines = table.lines
                    .Where(l => l != null && l.speaker == speaker && !string.IsNullOrWhiteSpace(textOf(l)))
                    .ToList();
                if (lines.Count == 0) continue;

                text.AppendLine($"=== {speaker.ToString().ToUpperInvariant()}: {VoiceSuggestion(speaker)} ===");
                text.AppendLine();
                foreach (VoiceLine line in lines)
                {
                    text.AppendLine(ClipName(prefix, line.key));
                    text.AppendLine(textOf(line));
                    text.AppendLine();
                }
            }
            return text.ToString();
        }

        static string VoiceSuggestion(Speaker speaker)
        {
            switch (speaker)
            {
                case Speaker.Teacher: return "use a warm female Multi voice";
                case Speaker.Kid: return "use a young voice";
                case Speaker.Goblin: return "use a silly voice";
                case Speaker.Alien: return "use a young voice, sped up a little for a squeaky alien";
                default: return "use a clear, friendly voice";
            }
        }

        // ----- Linking generated clips ---------------------------------------------------------

        public sealed class LinkReport
        {
            public int Total;
            public int Linked;
            public readonly List<string> MissingDutch = new List<string>();
            public readonly List<string> MissingEnglish = new List<string>();
        }

        [MenuItem("CoolSchool/Voice/Link Generated Clips")]
        static void LinkMenu()
        {
            LineTable table = VoiceLineSeeder.LoadOrCreate();
            LinkReport report = LinkClips(table);

            string message = $"Linked {report.Linked} new clips ({report.Total} lines, Dutch and English).";
            if (report.MissingDutch.Count > 0)
                message += $"\n\nMissing Dutch ({report.MissingDutch.Count}):\n" + Summarise(report.MissingDutch);
            if (report.MissingEnglish.Count > 0)
                message += $"\n\nMissing English ({report.MissingEnglish.Count}):\n" + Summarise(report.MissingEnglish);

            // The console always gets the full list.
            Debug.Log("CoolSchool: " + string.Join(" ", new[] { $"Linked {report.Linked} new clips.",
                "Missing Dutch: " + string.Join(", ", report.MissingDutch),
                "Missing English: " + string.Join(", ", report.MissingEnglish) }));
            EditorUtility.DisplayDialog("CoolSchool voice", message, "OK");
        }

        static string Summarise(List<string> names)
        {
            const int shown = 12;
            string text = string.Join("\n", names.Take(shown));
            return names.Count > shown ? text + $"\n... and {names.Count - shown} more (see the Console)" : text;
        }

        /// <summary>Assigns every AudioClip named like a line's file name. Existing links stay if no clip is found.</summary>
        public static LinkReport LinkClips(LineTable table)
        {
            var clipsByName = new Dictionary<string, AudioClip>();

            // The generated clips folder is searched first, so a generated clip wins over a same-named clip elsewhere.
            var guids = new List<string>();
            if (AssetDatabase.IsValidFolder(GeneratedClipsFolder))
                guids.AddRange(AssetDatabase.FindAssets("t:AudioClip", new[] { GeneratedClipsFolder }));
            guids.AddRange(AssetDatabase.FindAssets("t:AudioClip"));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string clipName = Path.GetFileNameWithoutExtension(path);
                if (clipsByName.ContainsKey(clipName)) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clipsByName[clipName] = clip;
            }

            var report = new LinkReport();
            foreach (VoiceLine line in table.lines)
            {
                if (line == null) continue;

                report.Total++;
                string dutchName = ClipName(DutchPrefix, line.key);
                string englishName = ClipName(EnglishPrefix, line.key);
                if (!Link(clipsByName, dutchName, ref line.dutchClip, report)) report.MissingDutch.Add(dutchName);
                if (!Link(clipsByName, englishName, ref line.englishClip, report)) report.MissingEnglish.Add(englishName);
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>Returns true when the line now has a clip (found by name, or linked earlier).</summary>
        static bool Link(Dictionary<string, AudioClip> clips, string clipName, ref AudioClip target, LinkReport report)
        {
            if (clips.TryGetValue(clipName, out AudioClip found))
            {
                if (target != found) report.Linked++;
                target = found;
                return true;
            }
            return target != null;
        }
    }
}
