using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using CoolSchool.Voice;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Generates every missing voice clip through the DeepVoice server, one request at a time, saves them as
    /// Assets/CoolSchool/Voice/Clips/&lt;lang&gt;/&lt;lang&gt;_&lt;key&gt;.mp3 and links them into the line table.
    /// Works from the menu and from the command line (see Tools/unity-batch.ps1).
    /// The invoice number is never written to the repo and never logged.
    /// </summary>
    public static class VoiceBatchGenerator
    {
        const string Url = "http://50.19.203.25:5000/invoice";
        const string ModelName = "DeepVoice_Multi";
        const string FallbackVoice = "Lily";
        const string ClipsFolder = VoiceLineExporter.GeneratedClipsFolder;
        const string SettingsPath = "Assets/CoolSchool/Voice/VoiceGenerationSettings.asset";
        const string InvoicePrefsKey = "DeepVoice_Invoice";
        const string InvoiceArg = "-deepVoiceInvoice";
        const string LanguageArg = "-voiceLanguage";

        // The service terms forbid overloading the server: one request at a time, with a pause between them.
        const float PauseSeconds = 1.6f;
        const int TimeoutSeconds = 90;

        sealed class Summary
        {
            public int Generated;
            public int Failed;
            public int Skipped;
            public int Characters;
            public bool Fatal;
            public bool Cancelled;
        }

        enum LineResult { Ok, Failed, Fatal }

        // ----- Entry points --------------------------------------------------------------------

        [MenuItem("CoolSchool/Voice/Generate Missing Lines (DeepVoice)")]
        static void GenerateMenu()
        {
            Run(GameLanguage.Dutch);
        }

        [MenuItem("CoolSchool/Voice/Generate Missing Lines (DeepVoice, English)")]
        static void GenerateEnglishMenu()
        {
            Run(GameLanguage.English);
        }

        /// <summary>For -executeMethod. Language from "-voiceLanguage en" (default Dutch). Exits 0 on success, 1 on a fatal error.</summary>
        public static void GenerateMissingFromCommandLine()
        {
            int code = 1;
            try
            {
                Summary summary = Run(ParseLanguage());
                code = summary.Fatal || summary.Cancelled ? 1 : 0;
            }
            catch (Exception e)
            {
                Debug.LogError("CoolSchool voice: unexpected error: " + e);
            }
            EditorApplication.Exit(code);
        }

        // ----- The batch -----------------------------------------------------------------------

        static Summary Run(GameLanguage language)
        {
            var summary = new Summary();
            string prefix = language == GameLanguage.English ? "en" : "nl";

            string invoice = FindInvoice();
            if (string.IsNullOrWhiteSpace(invoice))
            {
                Debug.LogError("CoolSchool voice: no DeepVoice invoice number found. Provide it in one of three ways:\n"
                    + $"1. command line: {InvoiceArg} <number>  (Unity echoes its arguments into the log, so prefer 2 or 3)\n"
                    + "2. the DeepVoice window in Unity, which stores the invoice in PlayerPrefs 'DeepVoice_Invoice'\n"
                    + "3. the first line of CoolSchool/UserSettings/DeepVoiceInvoice.txt (git-ignored)");
                summary.Fatal = true;
                return summary;
            }

            // CreateOrUpdate (not just LoadOrCreate) so seed lines added since the asset was made are included. Clips are never cleared.
            LineTable table = VoiceLineSeeder.CreateOrUpdate();
            VoiceGenerationSettings settings = LoadOrCreateSettings();

            var todo = new List<VoiceLine>();
            foreach (VoiceLine line in table.lines)
            {
                if (line == null || string.IsNullOrEmpty(line.key)) continue;
                if (line.ClipFor(language) != null) continue;
                if (string.IsNullOrWhiteSpace(language == GameLanguage.English ? line.english : line.dutch))
                {
                    summary.Skipped++;
                    continue;
                }
                todo.Add(line);
            }

            Debug.Log($"CoolSchool voice: {todo.Count} missing {language} line(s) of {table.lines.Count}.");

            // The DeepVoice server is plain http; the project forbids that, so relax it for this run only.
            InsecureHttpOption originalInsecureHttp = PlayerSettings.insecureHttpOption;
            if (originalInsecureHttp == InsecureHttpOption.NotAllowed)
            {
                PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
                Debug.Log("CoolSchool voice: PlayerSettings.insecureHttpOption temporarily relaxed to DevelopmentOnly for the DeepVoice http endpoint.");
            }

            try
            {
                for (int i = 0; i < todo.Count; i++)
                {
                    VoiceLine line = todo[i];
                    string text = Clean(language == GameLanguage.English ? line.english : line.dutch);
                    string clipName = VoiceLineExporter.ClipName(prefix + "_", line.key);
                    string assetPath = $"{ClipsFolder}/{prefix}/{clipName}.mp3";

                    if (File.Exists(AbsolutePath(assetPath)))
                    {
                        // Generated earlier, just not linked yet. The link step below picks it up.
                        summary.Skipped++;
                        Debug.Log($"CoolSchool voice: [{i + 1}/{todo.Count}] {line.key} already exists, skipped.");
                        continue;
                    }

                    if (ShowProgress(i, todo.Count, line.key))
                    {
                        summary.Cancelled = true;
                        Debug.LogWarning("CoolSchool voice: cancelled.");
                        break;
                    }

                    string voice = VoiceFor(settings, line.speaker);
                    var timer = Stopwatch.StartNew();
                    LineResult result = Generate(text, voice, invoice, settings, assetPath);
                    timer.Stop();

                    if (result == LineResult.Ok)
                    {
                        summary.Generated++;
                        summary.Characters += text.Length;
                        Debug.Log($"CoolSchool voice: [{i + 1}/{todo.Count}] {line.key} ({voice}) {text.Length} chars, {timer.Elapsed.TotalSeconds:0.0}s");
                    }
                    else
                    {
                        summary.Failed++;
                        if (result == LineResult.Fatal)
                        {
                            summary.Fatal = true;
                            break;
                        }
                    }

                    if (i < todo.Count - 1 && !Pause(i, todo.Count, line.key))
                    {
                        summary.Cancelled = true;
                        Debug.LogWarning("CoolSchool voice: cancelled.");
                        break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // Link whatever exists now, even after a failure, so no generated clip is left unlinked.
                AssetDatabase.Refresh();
                VoiceLineExporter.LinkReport report = VoiceLineExporter.LinkClips(table);
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
                Debug.Log($"CoolSchool voice: linked {report.Linked} new clip(s).");

                PlayerSettings.insecureHttpOption = originalInsecureHttp;
            }

            Debug.Log($"CoolSchool voice: DONE {language}. generated {summary.Generated}, failed {summary.Failed}, "
                + $"skipped {summary.Skipped}, characters sent {summary.Characters}"
                + (summary.Fatal ? " (STOPPED by a fatal error)" : summary.Cancelled ? " (cancelled)" : "") + ".");
            return summary;
        }

        /// <summary>One blocking request. Fatal = stop the whole batch.</summary>
        static LineResult Generate(string text, string voice, string invoice, VoiceGenerationSettings settings, string assetPath)
        {
            string json = BuildJson(text, voice, invoice, settings.variability, settings.clarity);

            using (var request = new UnityWebRequest(Url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;

                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                while (!op.isDone) Thread.Sleep(50);

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogError($"CoolSchool voice: network failure ({request.error}). Stopping the batch.");
                    return LineResult.Fatal;
                }
                if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    if (request.responseCode == 400)
                    {
                        Debug.LogError("CoolSchool voice: the server rejected the text (HTTP 400). Skipping this line.");
                        return LineResult.Failed;
                    }
                    Debug.LogError($"CoolSchool voice: server error HTTP {request.responseCode}. Stopping the batch.");
                    return LineResult.Fatal;
                }

                string body = request.downloadHandler.text ?? string.Empty;
                if (body == "Invalid Response")
                {
                    Debug.LogError("CoolSchool voice: invalid invoice/order number. Stopping the batch.");
                    return LineResult.Fatal;
                }
                if (body == "Limit Reached")
                {
                    Debug.LogError("CoolSchool voice: the DeepVoice character limit is reached. Stopping the batch. The count renews on the 30th/31st.");
                    return LineResult.Fatal;
                }

                byte[] audio;
                try
                {
                    audio = Convert.FromBase64String(body.Trim());
                }
                catch (FormatException)
                {
                    string shown = body.Length > 120 ? body.Substring(0, 120) + "..." : body;
                    Debug.LogError($"CoolSchool voice: unexpected server answer (not audio): {shown}");
                    return LineResult.Failed;
                }
                if (audio.Length < 100)
                {
                    Debug.LogError("CoolSchool voice: the server returned almost no audio. Skipping this line.");
                    return LineResult.Failed;
                }

                SaveClip(assetPath, audio);
                return LineResult.Ok;
            }
        }

        static void SaveClip(string assetPath, byte[] mp3)
        {
            string absolute = AbsolutePath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, mp3);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is AudioImporter importer)
            {
                importer.forceToMono = true;
                AudioImporterSampleSettings sample = importer.defaultSampleSettings;
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.Vorbis;
                sample.quality = 0.7f;
                importer.defaultSampleSettings = sample;
                importer.SaveAndReimport();
            }
        }

        // ----- JSON ----------------------------------------------------------------------------

        /// <summary>Same body as DeepVoice's own window (the numbers are strings there too), but with proper escaping.</summary>
        static string BuildJson(string text, string voice, string invoice, float variability, float clarity)
        {
            var sb = new StringBuilder();
            sb.Append("{\"text\":").Append(Quote(text));
            sb.Append(",\"model\":").Append(Quote(ModelName));
            sb.Append(",\"invoice\":").Append(Quote(invoice));
            sb.Append(",\"name\":").Append(Quote(voice));
            sb.Append(",\"variability\":").Append(Quote(variability.ToString(CultureInfo.InvariantCulture)));
            sb.Append(",\"clarity\":").Append(Quote(clarity.ToString(CultureInfo.InvariantCulture)));
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>A JSON string literal: backslash, quote and control characters escaped; non-ASCII written as \uXXXX.</summary>
        static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7E) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>Newlines and other whitespace runs become a single space.</summary>
        static string Clean(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool lastSpace = false;
            foreach (char c in text.Trim())
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    if (!lastSpace) sb.Append(' ');
                    lastSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastSpace = false;
                }
            }
            return sb.ToString();
        }

        // ----- Invoice, language, settings -----------------------------------------------------

        /// <summary>1. command line, 2. PlayerPrefs, 3. first line of UserSettings/DeepVoiceInvoice.txt. Never logged.</summary>
        static string FindInvoice()
        {
            string fromArg = GetArg(InvoiceArg);
            if (!string.IsNullOrWhiteSpace(fromArg)) return fromArg.Trim();

            string fromPrefs = PlayerPrefs.GetString(InvoicePrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(fromPrefs)) return fromPrefs.Trim();

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string file = Path.Combine(projectRoot, "UserSettings", "DeepVoiceInvoice.txt");
                if (File.Exists(file))
                {
                    using (var reader = new StreamReader(file))
                    {
                        string first = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(first)) return first.Trim();
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return null;
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static GameLanguage ParseLanguage()
        {
            string value = GetArg(LanguageArg);
            if (value != null && value.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return GameLanguage.English;
            return GameLanguage.Dutch;
        }

        static VoiceGenerationSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<VoiceGenerationSettings>(SettingsPath);
            if (settings != null) return settings;

            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool", "Voice");
            settings = ScriptableObject.CreateInstance<VoiceGenerationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>The configured voice for a speaker if it exists in DeepVoice's Voice enum, otherwise Lily.</summary>
        static string VoiceFor(VoiceGenerationSettings settings, Speaker speaker)
        {
            string configured;
            switch (speaker)
            {
                case Speaker.Kid: configured = settings.kidVoice; break;
                case Speaker.Goblin: configured = settings.goblinVoice; break;
                case Speaker.Narrator: configured = settings.narratorVoice; break;
                default: configured = settings.teacherVoice; break;
            }

            if (!string.IsNullOrWhiteSpace(configured))
            {
                // Read from DeepVoice's own enum (found by reflection, so this compiles without touching the third-party code).
                foreach (string name in DeepVoiceNames())
                    if (string.Equals(name, configured.Trim(), StringComparison.OrdinalIgnoreCase)) return name;
            }

            Debug.LogWarning($"CoolSchool voice: voice '{configured}' for {speaker} is not a DeepVoice voice, using {FallbackVoice}.");
            return FallbackVoice;
        }

        static string[] deepVoiceNames;

        /// <summary>The names in AiKodexDeepVoice.DeepVoiceEditor.Voice. Only Lily when DeepVoice is not in the project.</summary>
        static string[] DeepVoiceNames()
        {
            if (deepVoiceNames != null) return deepVoiceNames;

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AiKodexDeepVoice.DeepVoiceEditor+Voice", false);
                if (type != null && type.IsEnum)
                {
                    deepVoiceNames = Enum.GetNames(type);
                    return deepVoiceNames;
                }
            }

            Debug.LogWarning("CoolSchool voice: DeepVoice's Voice enum was not found, only Lily is available.");
            deepVoiceNames = new[] { FallbackVoice };
            return deepVoiceNames;
        }

        // ----- Progress and helpers ------------------------------------------------------------

        static string AbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        /// <summary>Returns true when the user pressed Cancel (GUI only).</summary>
        static bool ShowProgress(int index, int total, string key)
        {
            if (Application.isBatchMode) return false;
            return EditorUtility.DisplayCancelableProgressBar("CoolSchool voice (DeepVoice)",
                $"Generating {key} ({index + 1}/{total})", (float)index / Mathf.Max(1, total));
        }

        /// <summary>Waits between two requests. Returns false when the user cancelled.</summary>
        static bool Pause(int index, int total, string key)
        {
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < PauseSeconds)
            {
                Thread.Sleep(50);
                if (!Application.isBatchMode
                    && EditorUtility.DisplayCancelableProgressBar("CoolSchool voice (DeepVoice)",
                        $"Done {key} ({index + 1}/{total}), waiting before the next request", (float)(index + 1) / Mathf.Max(1, total)))
                    return false;
            }
            return true;
        }
    }
}
