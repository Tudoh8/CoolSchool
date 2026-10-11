using UnityEngine;

namespace CoolSchool.Audio
{
    /// <summary>
    /// Lets a UnityEvent play a sound cue: add this to an object and wire the event to Play with the cue name.
    /// </summary>
    public class SfxCue : MonoBehaviour
    {
        [Tooltip("On: the sound comes from this object's place in the world. Off: it plays flat.")]
        [SerializeField] bool atThisPosition;

        public void Play(string cue)
        {
            if (atThisPosition) Sfx.Play(cue, transform.position);
            else Sfx.Play(cue);
        }
    }
}
