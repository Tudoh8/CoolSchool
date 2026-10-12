using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The touch (and mouse) part of counting the gold. A press picks the coin that is nearest to the finger on the screen,
    /// as long as it is close enough, so small fingers never have to be precise. The picked coin is reported once; whoever
    /// listens flies it into the chest. Counting needs one press per coin, never a drag.
    /// </summary>
    public class TreasureCoinPicker : MonoBehaviour
    {
        [SerializeField] Camera cam;

        // How close (in pixels) a press must be to a coin. Scaled up on screens with many dots per inch, so it is a finger-sized
        // distance on a tablet too; never smaller than this on a low-resolution editor window.
        const float PickPixels = 110f;
        const float ReferenceDpi = 160f;
        const float MaxDpiScale = 2.5f;

        /// <summary>True while presses pick coins.</summary>
        public bool Armed { get; set; }

        /// <summary>Raised once for every coin that is picked.</summary>
        public event System.Action<TreasureCoin> Picked;

        TreasureCoin[] coins = new TreasureCoin[0];
        readonly HashSet<TreasureCoin> alreadyPicked = new HashSet<TreasureCoin>();

        /// <summary>The coins that can be picked. Coins picked before are forgotten.</summary>
        public void SetCoins(TreasureCoin[] newCoins)
        {
            coins = newCoins ?? new TreasureCoin[0];
            alreadyPicked.Clear();
        }

        void Update()
        {
            if (!Armed || coins.Length == 0) return;

            // A tap on the parent panel is not a tap on a coin.
            if (CoolSchool.Parent.ParentGate.InputBlocked) return;

            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            TreasureCoin coin = Nearest(pointer.position.ReadValue());
            if (coin == null) return;

            alreadyPicked.Add(coin);
            Picked?.Invoke(coin);
        }

        /// <summary>The coin not yet collected whose middle is nearest to the press, if it is within a finger's reach.</summary>
        TreasureCoin Nearest(Vector2 press)
        {
            Camera view = cam != null ? cam : Camera.main;
            if (view == null) return null;

            float best = PickRadius();
            TreasureCoin nearest = null;
            foreach (TreasureCoin coin in coins)
            {
                if (coin == null || coin.Collected || alreadyPicked.Contains(coin) || !coin.gameObject.activeInHierarchy) continue;

                Vector3 point = view.WorldToScreenPoint(coin.transform.position);
                if (point.z <= 0f) continue;

                float distance = Vector2.Distance(press, point);
                if (distance >= best) continue;

                best = distance;
                nearest = coin;
            }
            return nearest;
        }

        static float PickRadius()
        {
            float dpi = Screen.dpi;
            float scale = dpi > 0f ? Mathf.Clamp(dpi / ReferenceDpi, 1f, MaxDpiScale) : 1f;
            return PickPixels * scale;
        }
    }
}
