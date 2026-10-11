using System.Collections;
using UnityEngine;

namespace CoolSchool.Voice
{
    /// <summary>Says a line a little while after the scene starts, for example the teacher's welcome.</summary>
    public class SayOnStart : MonoBehaviour
    {
        [SerializeField] string group = "general.welcome";
        [SerializeField] float delaySeconds = 1f;

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            VoicePlayer.Say(group);
        }
    }
}
