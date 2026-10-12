using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The import settings for every clip under Assets/Imported/Audio/ (Android tablet): ambience loops stream as stereo,
    /// everything else is decompressed on load as mono. Used by ImportedAudioPostprocessor for new files and by
    /// SoundPackPicks for clips that are already imported.
    /// </summary>
    public static class ImportedAudioSettings
    {
        public const string Root = "Assets/Imported/Audio/";
        const string AmbientFolder = "/Ambient Loops/";
        const int SampleRate = 44100;
        const float AmbientQuality = 0.5f;
        const float EffectQuality = 0.6f;

        public static bool IsImportedPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(Root, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sets the importer to the wanted settings. Returns true when something changed (the caller must reimport).</summary>
        public static bool Apply(AudioImporter importer)
        {
            if (importer == null || !IsImportedPath(importer.assetPath)) return false;

            bool ambient = importer.assetPath.IndexOf(AmbientFolder, System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool changed = false;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType loadType = ambient ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            float quality = ambient ? AmbientQuality : EffectQuality;

            if (settings.loadType != loadType) { settings.loadType = loadType; changed = true; }
            if (settings.compressionFormat != AudioCompressionFormat.Vorbis) { settings.compressionFormat = AudioCompressionFormat.Vorbis; changed = true; }
            if (!Mathf.Approximately(settings.quality, quality)) { settings.quality = quality; changed = true; }
            if (settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate) { settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; changed = true; }
            if (settings.sampleRateOverride != SampleRate) { settings.sampleRateOverride = SampleRate; changed = true; }
            if (!ambient && !settings.preloadAudioData) { settings.preloadAudioData = true; changed = true; }
            if (changed) importer.defaultSampleSettings = settings;

            // Ambience keeps its stereo and streams from disk; effects are mono and ready in memory.
            bool mono = !ambient;
            if (importer.forceToMono != mono) { importer.forceToMono = mono; changed = true; }

            return changed;
        }
    }

    /// <summary>Applies ImportedAudioSettings to every new audio file under Assets/Imported/Audio/.</summary>
    public class ImportedAudioPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!ImportedAudioSettings.IsImportedPath(assetPath)) return;
            if (assetImporter is AudioImporter importer) ImportedAudioSettings.Apply(importer);
        }
    }
}
