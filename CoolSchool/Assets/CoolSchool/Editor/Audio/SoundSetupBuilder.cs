using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Adds the scene's "Sfx" object, wired to the sound library. The AudioListener stays on the main camera.
    /// Called by the playground builder after the voice setup.
    /// </summary>
    public static class SoundSetupBuilder
    {
        public static GameObject Setup()
        {
            SoundLibrary library = SoundLibrarySeeder.CreateOrUpdate();

            var sfxObject = new GameObject("Sfx");
            var sfx = sfxObject.AddComponent<Sfx>();
            PlaygroundPrototypeBuilder.SetObject(sfx, "library", library);
            PlaygroundPrototypeBuilder.SetInt(sfx, "poolSize", 12);

            // Exactly one listener, on the camera.
            Camera main = Camera.main;
            if (main != null && main.GetComponent<AudioListener>() == null) main.gameObject.AddComponent<AudioListener>();
            return sfxObject;
        }
    }
}
