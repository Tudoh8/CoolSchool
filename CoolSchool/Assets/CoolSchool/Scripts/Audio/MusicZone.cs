using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Audio
{
    /// <summary>
    /// Starts a music cue when the player walks into a box or a circle, and optionally another one when he leaves.
    /// Not placed in the scene yet.
    /// </summary>
    public class MusicZone : MonoBehaviour
    {
        [SerializeField] string cue = "music.playground";
        [Tooltip("Played when the player leaves the zone. Leave empty to keep the music going.")]
        [SerializeField] string exitCue;
        [Tooltip("A box around this object. Leave at zero to use the radius instead.")]
        [SerializeField] Vector3 boxSize = Vector3.zero;
        [SerializeField] float radius = 10f;

        PlayerController player;
        bool inside;

        void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            bool nowInside = Contains(player.transform.position);
            if (nowInside == inside) return;

            inside = nowInside;
            if (inside) Sfx.PlayMusic(cue);
            else if (!string.IsNullOrEmpty(exitCue)) Sfx.PlayMusic(exitCue);
        }

        void OnDisable()
        {
            inside = false;
        }

        bool Contains(Vector3 worldPosition)
        {
            if (boxSize != Vector3.zero)
            {
                Vector3 local = transform.InverseTransformPoint(worldPosition);
                Vector3 half = boxSize * 0.5f;
                return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
            }

            Vector3 flat = worldPosition - transform.position;
            flat.y = 0f;
            return flat.sqrMagnitude <= radius * radius;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            if (boxSize != Vector3.zero)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, boxSize);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, radius);
            }
        }
    }
}
