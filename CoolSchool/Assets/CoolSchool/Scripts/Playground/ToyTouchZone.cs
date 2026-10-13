using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// On the "TouchZone" child of a toy (a trigger BoxCollider around it). Tells the toy when the player walks into it and out of it.
    /// Like ActivityZone it does not wait for physics events (the character controller is switched off while riding): it checks the
    /// player's position against the box every frame.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ToyTouchZone : MonoBehaviour
    {
        [SerializeField] PlaygroundToy toy;

        BoxCollider box;
        bool inside = true;   // Assume inside until he has been seen outside once: a toy never starts by itself at the start of the game.
        bool known;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            if (toy == null) toy = GetComponentInParent<PlaygroundToy>();
        }

        void OnDisable()
        {
            known = false;
            inside = true;
        }

        void Update()
        {
            if (toy == null) return;
            if (box == null) box = GetComponent<BoxCollider>();
            if (box == null) return;

            ToyPlayer toyPlayer = ToyPlayer.Instance;
            PlayerController player = toyPlayer != null ? toyPlayer.Player : null;
            if (player == null) return;

            bool now = Contains(player.transform.position);
            if (known && now == inside) return;

            known = true;
            inside = now;
            if (now) toy.PlayerEntered(player);
            else toy.PlayerExited(player);
        }

        /// <summary>True when the world point is inside the zone's box.</summary>
        public bool Contains(Vector3 worldPoint)
        {
            BoxCollider b = box != null ? box : GetComponent<BoxCollider>();
            if (b == null) return false;

            Vector3 local = transform.InverseTransformPoint(worldPoint) - b.center;
            Vector3 half = b.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }
}
