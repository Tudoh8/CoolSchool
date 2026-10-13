using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Parent;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.ToyRoom;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The pit lane beside the race start (contract Docs/specs/cart-garage-contract.md): a row of parking bays, one for each cart body,
    /// where every body stands. A body he owns shows as the real model, one he has not won yet as a black silhouette, so he can see what
    /// there is to win. It reuses the Toy Room's display slots (<see cref="ToyShelfSlot"/>): the builder places one slot in each bay and
    /// this component tells each slot what to show, whenever his progress changes. When he walks past a body he owns it hops and says its
    /// name, once per visit (the same rules as the Toy Room shelf: never while another game runs, never over a voice line, and not more
    /// often than every couple of seconds).
    /// </summary>
    public class PitLane : MonoBehaviour
    {
        [SerializeField] RewardCatalog catalog;
        [SerializeField] ToyShelfSlot[] slots;
        [Tooltip("World-space box around the bays (x/z tested): while he is in it the bodies greet him.")]
        [SerializeField] Bounds area;
        [SerializeField] float talkRadius = 1.5f;

        const float MinSecondsBetweenNames = 2.5f;

        PlayerController player;
        bool visiting;
        float lastNameTime = float.NegativeInfinity;
        ToyShelfSlot near;
        readonly HashSet<ToyShelfSlot> greeted = new HashSet<ToyShelfSlot>();

        /// <summary>The slots in the bays, in the order of the bays (the preview shots use them).</summary>
        public IReadOnlyList<ToyShelfSlot> Slots => slots;

        void OnEnable()
        {
            ProgressStore.Changed += OnProgressChanged;
            Refresh(ProgressStore.Current);
        }

        void OnDisable()
        {
            ProgressStore.Changed -= OnProgressChanged;
            visiting = false;
        }

        void OnProgressChanged()
        {
            Refresh(ProgressStore.Current);
        }

        /// <summary>
        /// Shows every bay for this progress: the model of the body when it is owned, the black silhouette when it is not. Also works in
        /// edit mode (the preview pictures use it with a fresh or a full progress).
        /// </summary>
        public void Refresh(PlayerProgress progress)
        {
            if (catalog == null || slots == null) return;
            if (progress == null) progress = new PlayerProgress();

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
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            Vector3 point = player.transform.position;
            bool inside = area.Contains(new Vector3(point.x, area.center.y, point.z));
            if (!inside)
            {
                // Leaving the lane: next time every body greets him again.
                if (visiting)
                {
                    visiting = false;
                    greeted.Clear();
                    near = null;
                }
                return;
            }

            visiting = true;
            if (!ParentGate.InputBlocked) GreetNearest(point);
        }

        /// <summary>The nearest owned body within talkRadius hops when he comes close and says its name (once per visit).</summary>
        void GreetNearest(Vector3 point)
        {
            ToyShelfSlot nearest = NearestOwnedSlot(point);
            if (nearest != near)
            {
                near = nearest;
                if (near != null) near.Hop();
            }

            if (near == null || greeted.Contains(near)) return;
            if (!CanSpeakNow()) return;

            string group = ItemVoice.GroupFor(near.Item);
            greeted.Add(near);
            if (!string.IsNullOrEmpty(group)) VoicePlayer.Say(group);
            lastNameTime = Time.time;
            ProgressStore.MarkSeen(near.ItemId);
        }

        bool CanSpeakNow()
        {
            if (Time.time - lastNameTime < MinSecondsBetweenNames) return false;

            // Not while a game has taken over (the race, the castle...): he is in the default free play when he walks past the cars.
            ActivityManager manager = ActivityManager.Instance;
            if (manager != null && manager.Current != manager.DefaultActivity) return false;

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
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
