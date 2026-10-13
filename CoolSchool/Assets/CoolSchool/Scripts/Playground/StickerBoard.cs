using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.Audio;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The sticker board on the playground: one round spot per sticker (in ToyStickers.AllIds order), showing the toy's picture in colour
    /// once earned and as a black silhouette until then. Each spot is a transform with three SpriteRenderer children named Rim, Face and
    /// Icon. The board introduces itself with a narrator line the first time he comes near it in free play.
    /// </summary>
    public class StickerBoard : MonoBehaviour
    {
        [Tooltip("One spot for each sticker id, in the order of ToyStickers.AllIds.")]
        [SerializeField] Transform[] spots;
        [Tooltip("The toy pictures, in the same order as the spots.")]
        [SerializeField] Sprite[] icons;
        [Tooltip("Where he stands to look at the board (distance is measured from here).")]
        [SerializeField] Transform introPoint;
        [SerializeField] float introDistance = 3f;

        static readonly Color SilhouetteColour = new Color(0.04f, 0.04f, 0.06f, 1f);
        static readonly Color EmptyRim = new Color(0.62f, 0.60f, 0.58f, 1f);
        static readonly Color EmptyFace = new Color(0.86f, 0.85f, 0.82f, 1f);

        public static StickerBoard Instance { get; private set; }

        readonly HashSet<string> held = new HashSet<string>();
        readonly Dictionary<string, Coroutine> pops = new Dictionary<string, Coroutine>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void OnEnable()
        {
            Instance = this;
            ProgressStore.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            ProgressStore.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (ProgressStore.Current.toyBoardIntroHeard) return;

            // Only in free play and while nothing is going on, once ever.
            ActivityManager manager = ActivityManager.Instance;
            if (manager == null || manager.Current != manager.DefaultActivity) return;

            ToyPlayer toyPlayer = ToyPlayer.Instance;
            if (toyPlayer == null || toyPlayer.Player == null || toyPlayer.IsPlaying || toyPlayer.Player.IsRiding) return;

            Vector3 here = toyPlayer.Player.transform.position;
            Vector3 centre = introPoint != null ? introPoint.position : transform.position;
            here.y = 0f;
            centre.y = 0f;
            if ((here - centre).sqrMagnitude > introDistance * introDistance) return;

            ProgressStore.Current.toyBoardIntroHeard = true;
            ProgressStore.Save();
            VoicePlayer.Say("toys.board_intro");
        }

        /// <summary>The spot of this sticker, or null if the id is unknown.</summary>
        public Transform SpotFor(string stickerId)
        {
            int index = System.Array.IndexOf(ToyStickers.AllIds, stickerId);
            return spots != null && index >= 0 && index < spots.Length ? spots[index] : null;
        }

        /// <summary>Shows the stickers he has and silhouettes for the rest. A sticker on hold (about to be placed) stays a silhouette.</summary>
        public void Refresh()
        {
            if (spots == null) return;

            for (int i = 0; i < spots.Length && i < ToyStickers.AllIds.Length; i++)
            {
                string id = ToyStickers.AllIds[i];
                if (pops.ContainsKey(id)) continue;

                bool owned = ToyStickers.Has(id) && !held.Contains(id);
                Apply(i, owned, 1f);
            }
        }

        /// <summary>Keeps this sticker a silhouette on the board until Release or PlaceSticker (it is flying to the jar first).</summary>
        internal void Hold(string stickerId)
        {
            held.Add(stickerId);
        }

        /// <summary>The sticker may show on the board now.</summary>
        internal void Release(string stickerId)
        {
            held.Remove(stickerId);
            Refresh();
        }

        /// <summary>True when the sticker's spot is in the camera's view (so placing it will be seen).</summary>
        internal bool IsOnScreen(string stickerId)
        {
            Transform spot = SpotFor(stickerId);
            Camera camera = Camera.main;
            if (spot == null || camera == null) return false;

            Vector3 view = camera.WorldToViewportPoint(spot.position);
            return view.z > 0f && view.x > 0.02f && view.x < 0.98f && view.y > 0.02f && view.y < 0.98f;
        }

        /// <summary>Pops the sticker into its spot with a sparkle.</summary>
        public IEnumerator PlaceSticker(string stickerId)
        {
            int index = System.Array.IndexOf(ToyStickers.AllIds, stickerId);
            Transform spot = SpotFor(stickerId);
            held.Remove(stickerId);
            if (spot == null || index < 0)
            {
                Refresh();
                yield break;
            }

            pops[stickerId] = null;
            Apply(index, true, 0f);
            BattleEffects.Sparkle(spot.position);
            Sfx.Play("toys.sticker_stick", spot.position);

            const float Seconds = 0.45f;
            for (float elapsed = 0f; elapsed < Seconds && spot != null; elapsed += Time.deltaTime)
            {
                float k = elapsed / Seconds;
                const float c1 = 1.70158f;
                float t = k - 1f;
                spot.localScale = Vector3.one * (1f + (c1 + 1f) * t * t * t + c1 * t * t);
                yield return null;
            }

            if (spot != null) spot.localScale = Vector3.one;
            pops.Remove(stickerId);
            Refresh();
        }

        void Apply(int index, bool owned, float scale)
        {
            Transform spot = spots[index];
            if (spot == null) return;

            spot.localScale = Vector3.one * scale;

            SetSprite(spot, "Rim", ToyStickerArt.Disc, owned ? ToyStickerArt.RimColour(ToyStickers.AllIds[index]) : EmptyRim);
            SetSprite(spot, "Face", ToyStickerArt.Disc, owned ? Color.white : EmptyFace);
            Sprite icon = icons != null && index < icons.Length ? icons[index] : null;
            SetSprite(spot, "Icon", icon, owned ? Color.white : SilhouetteColour);
        }

        static void SetSprite(Transform spot, string childName, Sprite sprite, Color colour)
        {
            Transform child = spot.Find(childName);
            if (child == null) return;

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) return;

            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.enabled = sprite != null;
        }
    }
}
