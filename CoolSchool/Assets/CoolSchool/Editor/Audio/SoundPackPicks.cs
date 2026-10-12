using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CoolSchool.Audio;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Fills the sound library from Assets/CoolSchool/Audio/SoundPackPicks.txt: for every cue in that file the clips,
    /// volume and pitch range are replaced, and the imported clips get their Android import settings.
    /// The picks file wins over hand edits for the cues it lists; cues it does not list are left alone.
    /// Menu: CoolSchool/Audio/Apply Sound Pack Picks. Batch: -executeMethod CoolSchool.EditorTools.SoundPackPicks.ApplyFromCommandLine.
    /// </summary>
    public static class SoundPackPicks
    {
        public const string PicksPath = "Assets/CoolSchool/Audio/SoundPackPicks.txt";

        const float DefaultPitchMin = 0.95f;
        const float DefaultPitchMax = 1.05f;

        /// <summary>Pack key to project folder. Identical to the table in Tools/import-audio-clips.py.</summary>
        static readonly Dictionary<string, string> PackFolders = new Dictionary<string, string>
        {
            { "PM", "Assets/Imported/Audio/PhantomMagic/" },
            { "AR", "Assets/Imported/Audio/ActionRPG/" },
            { "ENV", "Assets/Imported/Audio/EnvironmentSounds/" },
            { "BES", "Assets/Imported/Audio/Bestiary/" },
            { "MON", "Assets/Imported/Audio/MonsterSounds/" },
            { "FEEL", "Assets/Imported/Audio/Feel/" },
            { "GUI", "Assets/Imported/Audio/UltimateCleanGUI/" },
            { "POLY", "Assets/polyperfect/Low Poly Animated Animals/Sounds/" },
        };

        [MenuItem("CoolSchool/Audio/Apply Sound Pack Picks")]
        static void ApplyMenu()
        {
            AssetDatabase.Refresh();
            SoundLibrary library = SoundLibrarySeeder.CreateOrUpdate();
            Debug.Log(Apply(library));
            Selection.activeObject = library;
        }

        /// <summary>Batch entry: creates or updates the library, applies the picks, logs the report, saves and exits.</summary>
        public static void ApplyFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                AssetDatabase.Refresh();
                SoundLibrary library = SoundLibrarySeeder.CreateOrUpdate();
                Debug.Log(Apply(library));
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Applies the picks file to the library and the clips' import settings. Returns the report.</summary>
        public static string Apply(SoundLibrary library)
        {
            var report = new StringBuilder();
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, PicksPath);
            if (library == null || !File.Exists(fullPath))
            {
                report.AppendLine($"CoolSchool: sound picks not applied ({(library == null ? "no sound library" : "missing " + PicksPath)}).");
                return report.ToString();
            }

            var lines = new List<string>();
            int cuesFilled = 0, clipsLoaded = 0, clipsMissing = 0, clipsReimported = 0;
            var seenClips = new HashSet<string>();

            string[] rows = File.ReadAllText(fullPath).Split('\n');
            for (int i = 0; i < rows.Length; i++)
            {
                string row = rows[i].TrimEnd('\r');
                if (row.Trim().Length == 0 || row.TrimStart().StartsWith("#")) continue;

                string[] fields = row.Split('\t');
                if (fields.Length < 5)
                {
                    lines.Add($"  line {i + 1}: expected 5 tab separated fields, found {fields.Length}");
                    continue;
                }

                string cue = fields[0].Trim();
                SoundEntry entry = library.Find(cue);
                if (entry == null)
                {
                    lines.Add($"  line {i + 1}: {cue} is not in the sound library");
                    continue;
                }

                float volume = ParseFloat(fields[1], 1f, cue, lines);
                float pitchMin = ParseFloat(fields[2], DefaultPitchMin, cue, lines);
                float pitchMax = ParseFloat(fields[3], DefaultPitchMax, cue, lines);

                var clips = new List<AudioClip>();
                foreach (string spec in fields[4].Split('|'))
                {
                    string clipSpec = spec.Trim();
                    if (clipSpec.Length == 0) continue;

                    string path = ToProjectPath(clipSpec);
                    if (path == null)
                    {
                        lines.Add($"  {cue}: unknown pack in '{clipSpec}'");
                        clipsMissing++;
                        continue;
                    }

                    AudioClip clip = LoadClip(path, seenClips, ref clipsReimported);
                    if (clip == null)
                    {
                        lines.Add($"  {cue}: missing {path}");
                        clipsMissing++;
                        continue;
                    }
                    clips.Add(clip);
                    clipsLoaded++;
                }

                // Nothing found (the pack is not imported yet): keep what the entry has.
                if (clips.Count == 0)
                {
                    lines.Add($"  {cue}: no clips found, entry left unchanged");
                    continue;
                }

                entry.clips = clips;
                entry.volume = Mathf.Clamp01(volume);
                entry.pitchRange = new Vector2(pitchMin, Mathf.Max(pitchMin, pitchMax));
                cuesFilled++;
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            report.AppendLine($"CoolSchool: sound picks applied: {cuesFilled} cues filled, {clipsLoaded} clips assigned, {clipsMissing} missing, {clipsReimported} clips reimported with new import settings.");
            foreach (string line in lines) report.AppendLine(line);
            return report.ToString();
        }

        /// <summary>"PM/Magic UI/Button.wav" becomes "Assets/Imported/Audio/PhantomMagic/Magic UI/Button.wav". Null for an unknown pack key.</summary>
        static string ToProjectPath(string spec)
        {
            int slash = spec.IndexOf('/');
            if (slash <= 0) return null;
            return PackFolders.TryGetValue(spec.Substring(0, slash).Trim(), out string folder) ? folder + spec.Substring(slash + 1).Trim() : null;
        }

        static float ParseFloat(string text, float fallback, string cue, List<string> lines)
        {
            text = text.Trim();
            if (text.Length == 0) return fallback;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) return value;

            lines.Add($"  {cue}: '{text}' is not a number, using {fallback.ToString(CultureInfo.InvariantCulture)}");
            return fallback;
        }

        /// <summary>Loads a clip and fixes its import settings once per run. Null when the file does not exist.</summary>
        static AudioClip LoadClip(string path, HashSet<string> seenClips, ref int reimported)
        {
            if (seenClips.Add(path) && ImportedAudioSettings.IsImportedPath(path))
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null && File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, path)))
                {
                    // On disk but not imported yet.
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(path) as AudioImporter;
                }

                if (importer != null && ImportedAudioSettings.Apply(importer))
                {
                    importer.SaveAndReimport();
                    reimported++;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
