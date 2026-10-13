using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Parent;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.ToyRoom
{
    /// <summary>
    /// The Toy Room's display: one per room, it owns every shelf slot and the medal board. It shows what he owns (real models) and
    /// what he does not (black silhouettes), sparkles the things he has not looked at yet, says hello to the item he walks past
    /// (a hop and its name, once per visit), and welcomes him when he walks in (the first time ever, new things, new medals).
    /// It refreshes itself whenever the progress changes.
    /// </summary>
    public class ToyShelf : MonoBehaviour
    {
        [SerializeField] RewardCatalog catalog;
        [SerializeField] ToyShelfSlot[] slots;
        [Tooltip("World-space box of the room's inside (x/z tested).")]
        [SerializeField] Bounds roomArea;
        [SerializeField] float talkRadius = 1.4f;
        [Tooltip("The wall board for the medals (optional).")]
        [SerializeField] MedalBoard medalBoard;
        [Tooltip("The gift icon and light beam in front of the door. Hidden while he is in the room or at its door (it would float over the room).")]
        [SerializeField] GameObject beacon;

        // The sparkles on unseen things repeat this often, and the room counts as "entered" a bit before the walls.
        const float SparkleSeconds = 1.6f;
        const float MinSecondsBetweenNames = 2.5f;
        const float WelcomeDelaySeconds = 0.6f;
        // The beacon hides this far before the door, like the roof does (BuildingInteriorReveal's approach distance).
        const float BeaconHideDistance = 3f;

        PlayerController player;
        bool inside;
        float nextSparkle;
        float lastNameTime = float.NegativeInfinity;
        float welcomeDoneTime;
        ToyShelfSlot near;
        readonly HashSet<ToyShelfSlot> greeted = new HashSet<ToyShelfSlot>();
        readonly HashSet<string> unseen = new HashSet<string>();
        bool welcomed;

        /// <summary>True while the player is inside the room.</summary>
        public bool PlayerInside => inside;

        void OnEnable()
        {
            ProgressStore.Changed += OnProgressChanged;
            Refresh(ProgressStore.Current);
        }

        void OnDisable()
        {
            ProgressStore.Changed -= OnProgressChanged;
            inside = false;
            if (beacon != null) beacon.SetActive(true);
        }

        void OnProgressChanged()
        {
            Refresh(ProgressStore.Current);
        }

        /// <summary>
        /// Shows every slot for this progress: owned items as models, the rest as silhouettes, and the medals he has seen.
        /// Also works in edit mode (the preview pictures use it with a fresh or a full progress).
        /// </summary>
        public void Refresh(PlayerProgress progress)
        {
            if (catalog == null || slots == null) return;
            if (progress == null) progress = new PlayerProgress();

            unseen.Clear();
            if (progress.unseenItems != null)
                foreach (string id in progress.unseenItems) unseen.Add(id);

            foreach (ToyShelfSlot slot in slots)
            {
                if (slot == null) continue;

                RewardItem item = catalog.Find(slot.ItemId);
                if (item == null)
                {
                    slot.Clear();
                    continue;
                }
                slot.Show(item, item.IsOwnedBy(progress));
            }

            if (medalBoard != null) medalBoard.Refresh(progress.medals, progress.medalsSeen);
        }

        /// <summary>
        /// True when something he has won and not looked at yet stands on one of this room's shelves. Cart things that have no slot
        /// (the paints, trails, horns...) live in the race's cart picker, so they are not news here; the race invites him for those.
        /// </summary>
        public bool HasUnseenOnShelf(PlayerProgress progress)
        {
            if (progress == null || progress.unseenItems == null || progress.unseenItems.Count == 0) return false;
            if (slots == null) return true;

            foreach (string id in progress.unseenItems)
            {
                foreach (ToyShelfSlot slot in slots)
                    if (slot != null && slot.ItemId == id) return true;
            }
            return false;
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            UpdateBeacon(player.transform.position);
            bool nowInside = IsInside(player.transform.position);
            if (nowInside && !inside) EnterRoom();
            else if (!nowInside && inside) LeaveRoom();
            inside = nowInside;
            if (!inside) return;

            if (Time.time >= nextSparkle) SparkleUnseen();
            if (!ParentGate.InputBlocked) GreetNearest();
        }

        /// <summary>The beacon shows again when he leaves; it hides while he is in the room or close to its door (on the south side).</summary>
        void UpdateBeacon(Vector3 point)
        {
            if (beacon == null) return;

            bool near = point.x >= roomArea.min.x && point.x <= roomArea.max.x
                && point.z >= roomArea.min.z - BeaconHideDistance && point.z <= roomArea.max.z;
            if (beacon.activeSelf == near) beacon.SetActive(!near);
        }

        bool IsInside(Vector3 point)
        {
            return point.x >= roomArea.min.x && point.x <= roomArea.max.x && point.z >= roomArea.min.z && point.z <= roomArea.max.z;
        }

        // ----- Walking in and out ------------------------------------------------------------------

        void EnterRoom()
        {
            greeted.Clear();
            near = null;
            welcomed = false;
            nextSparkle = Time.time + 0.8f;
            StartCoroutine(WelcomeRoutine());
        }

        void LeaveRoom()
        {
            StopAllCoroutines();
            greeted.Clear();
            near = null;
        }

        /// <summary>The welcome: the first time ever, or the new things, then the new medals popping onto the board.</summary>
        System.Collections.IEnumerator WelcomeRoutine()
        {
            yield return new WaitForSeconds(WelcomeDelaySeconds);

            PlayerProgress progress = ProgressStore.Current;
            if (!progress.toyRoomVisited)
            {
                VoicePlayer.Say("toyroom.welcome");
                progress.toyRoomVisited = true;
                ProgressStore.Save();
            }
            else if (HasUnseenOnShelf(progress))
            {
                VoicePlayer.Say("toyroom.new_item");
            }

            if (progress.medals > progress.medalsSeen)
            {
                VoicePlayer.Say("toyroom.medal");
                if (medalBoard != null) medalBoard.PopNewIn();
                ProgressStore.MarkMedalsSeen();
            }

            welcomed = true;
            welcomeDoneTime = Time.time;
        }

        // ----- Sparkles and greetings --------------------------------------------------------------

        void SparkleUnseen()
        {
            nextSparkle = Time.time + SparkleSeconds;
            if (unseen.Count == 0 || slots == null) return;

            bool first = true;
            foreach (ToyShelfSlot slot in slots)
            {
                if (slot == null || !slot.IsOwnedShown || !unseen.Contains(slot.ItemId)) continue;

                BattleEffects.Sparkle(slot.SparklePoint);
                if (first) Sfx.Play("toyroom.sparkle", slot.transform.position);
                first = false;
            }
        }

        /// <summary>The nearest owned slot within talkRadius hops when he comes close and says its name (once per visit).</summary>
        void GreetNearest()
        {
            ToyShelfSlot nearest = NearestOwnedSlot(player.transform.position);
            if (nearest != near)
            {
                near = nearest;
                if (near != null) near.Hop();
            }

            if (near == null || greeted.Contains(near)) return;
            if (!CanSpeakNow()) return;

            RewardItem item = near.Item;
            string group = ItemVoice.GroupFor(item);
            greeted.Add(near);
            if (!string.IsNullOrEmpty(group)) VoicePlayer.Say(group);
            lastNameTime = Time.time;
            ProgressStore.MarkSeen(near.ItemId);
        }

        bool CanSpeakNow()
        {
            if (!welcomed || Time.time - welcomeDoneTime < 0.5f) return false;
            if (Time.time - lastNameTime < MinSecondsBetweenNames) return false;

            // Not while another game has taken over (the wardrobe is fine: it is this room's own).
            ActivityManager manager = ActivityManager.Instance;
            if (manager != null && manager.Current != manager.DefaultActivity && !(manager.Current is ToyRoomWardrobe)) return false;

            VoicePlayer voice = VoicePlayer.Instance;
            return voice == null || !voice.IsBusy;
        }

        ToyShelfSlot NearestOwnedSlot(Vector3 point)
        {
            ToyShelfSlot best = null;
            float bestSqr = talkRadius * talkRadius;
            foreach (ToyShelfSlot slot in slots)
            {
                if (slot == null || !slot.IsOwnedShown) continue;

                Vector3 offset = slot.transform.position - point;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = slot;
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(roomArea.center, roomArea.size);
        }
    }
}
