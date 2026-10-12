using System.Collections;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.CameraRig;
using CoolSchool.Characters;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.ToyRoom
{
    /// <summary>
    /// "Wardrobe". The dress-up mirror in the Toy Room. Walking into the zone in front of the mirror puts him at the mirror
    /// facing the camera, the camera glides in so he fills the left of the screen, and a picture menu appears on the right
    /// (WardrobePanel). Tapping an owned card puts that item on him at once (the EquipmentApplier does the dressing; this class
    /// only chooses), a locked card wobbles. Done, or 45 seconds without a tap, ends it. He can never break anything: every
    /// choice can be changed again. OnEnd puts everything back at any moment (the back button may end it while the camera is
    /// gliding or the menu is open).
    /// </summary>
    public class ToyRoomWardrobe : Activity
    {
        [SerializeField] PlayerController player;
        [SerializeField] KidAnimator playerAnimator;
        [SerializeField] WeaponHolder playerWeapons;
        [SerializeField] BuddyFollower buddy;
        [SerializeField] FollowCamera followCamera;
        [Tooltip("In front of the mirror, facing the camera spot.")]
        [SerializeField] Transform standSpot;
        [Tooltip("Beside the stand spot: where the buddy waits while he dresses up.")]
        [SerializeField] Transform buddySpot;
        [Tooltip("Frames him full-body in the left part of the screen.")]
        [SerializeField] Transform cameraSpot;
        [Tooltip("Just outside the zone, so he does not walk straight back in.")]
        [SerializeField] Transform exitSpot;
        [SerializeField] WardrobePanel panel;
        [SerializeField] RewardCatalog catalog;
        [SerializeField] LessonUI lessonUI;
        [SerializeField] float idleEndSeconds = 45f;

        const float CameraGlideSeconds = 0.8f;
        const float LockedLineGapSeconds = 6f;
        const float DoneVoiceMaxSeconds = 3f;

        Coroutine routine;
        bool started;
        bool finishing;
        bool cameraMoved;
        bool hudHidden;
        bool savedJumpEnabled = true;
        float lastTapTime;
        float lastLockedLineTime = float.NegativeInfinity;
        Camera mainCamera;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (followCamera == null) followCamera = FindAnyObjectByType<FollowCamera>();
            if (lessonUI == null) lessonUI = FindAnyObjectByType<LessonUI>();
            if (player != null)
            {
                if (playerAnimator == null) playerAnimator = player.GetComponent<KidAnimator>();
                if (playerWeapons == null) playerWeapons = player.GetComponent<WeaponHolder>();
            }

            if (player == null || standSpot == null || cameraSpot == null || exitSpot == null || panel == null)
            {
                Debug.LogWarning("CoolSchool: the wardrobe is missing the player, its spots or its panel, so it can not start.");
                End();
                return;
            }

            started = true;
            finishing = false;
            cameraMoved = false;
            mainCamera = Camera.main;

            savedJumpEnabled = player.JumpEnabled;
            player.InputEnabled = false;
            player.JumpEnabled = false;
            if (lessonUI != null)
            {
                lessonUI.SetGameplayHudVisible(false);
                hudHidden = true;
            }

            BattleEffects.Sparkle(player.transform.position + Vector3.up * 0.8f);
            MovePlayer(standSpot.position, standSpot.rotation);
            if (playerWeapons != null) playerWeapons.ShowWeapons(true);
            if (buddy != null && buddySpot != null) buddy.HoldAt(buddySpot);

            panel.ItemChosen += OnItemChosen;
            panel.LockedChosen += OnLockedChosen;
            panel.Done += OnDone;

            routine = StartCoroutine(WardrobeRoutine());
        }

        protected override void OnEnd()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            StopAllCoroutines();

            if (panel != null)
            {
                panel.ItemChosen -= OnItemChosen;
                panel.LockedChosen -= OnLockedChosen;
                panel.Done -= OnDone;
                panel.Hide();
            }

            if (!started) return;
            started = false;

            if (playerWeapons != null) playerWeapons.ShowWeapons(false);
            if (buddy != null) buddy.ReleaseHold();

            // The player first, so the follow camera snaps to where he really is.
            if (player != null && exitSpot != null) MovePlayer(exitSpot.position, exitSpot.rotation);
            if (followCamera != null)
            {
                followCamera.enabled = true;
                followCamera.Snap();
            }
            cameraMoved = false;

            if (hudHidden && lessonUI != null) lessonUI.SetGameplayHudVisible(true);
            hudHidden = false;
            if (player != null)
            {
                player.InputEnabled = true;
                player.JumpEnabled = savedJumpEnabled;
            }
        }

        void OnDisable()
        {
            // If this object is switched off mid-way, put everything back first.
            if (IsRunning && gameObject.scene.isLoaded) End();
        }

        // ----- The flow --------------------------------------------------------------------------

        IEnumerator WardrobeRoutine()
        {
            if (followCamera != null) followCamera.enabled = false;
            yield return GlideCamera(cameraSpot);

            panel.Show();
            lastTapTime = Time.time;
            VoicePlayer.Say("wardrobe.welcome");

            while (!finishing)
            {
                if (Time.time - lastTapTime > idleEndSeconds)
                {
                    yield return FinishRoutine();
                    yield break;
                }
                yield return null;
            }
        }

        IEnumerator FinishRoutine()
        {
            finishing = true;

            panel.Hide();
            VoicePlayer.Say("wardrobe.done");
            if (playerAnimator != null) playerAnimator.PlayCheer();
            BattleEffects.Confetti(player.transform.position + Vector3.up * 1.2f);

            yield return new WaitForSeconds(0.4f);
            VoicePlayer voice = VoicePlayer.Instance;
            float giveUp = Time.time + DoneVoiceMaxSeconds;
            while (voice != null && voice.IsBusy && Time.time < giveUp) yield return null;

            routine = null;
            End();
        }

        /// <summary>The camera glides from where it is to the spot (the follow camera sleeps meanwhile).</summary>
        IEnumerator GlideCamera(Transform spot)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null || spot == null) yield break;

            cameraMoved = true;
            Transform cam = mainCamera.transform;
            Vector3 startPosition = cam.position;
            Quaternion startRotation = cam.rotation;
            for (float t = 0f; t < CameraGlideSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / CameraGlideSeconds);
                cam.SetPositionAndRotation(Vector3.Lerp(startPosition, spot.position, k), Quaternion.Slerp(startRotation, spot.rotation, k));
                yield return null;
            }
            cam.SetPositionAndRotation(spot.position, spot.rotation);
        }

        // ----- Taps on the panel -----------------------------------------------------------------

        void OnItemChosen(EquipSlot slot, RewardItem item)
        {
            if (!IsRunning || finishing) return;
            lastTapTime = Time.time;

            ProgressStore.Equip(slot, item != null ? item.id : string.Empty);
            if (item != null) ProgressStore.MarkSeen(item.id);
            panel.RefreshWorn();

            Vector3 chest = player.transform.position + Vector3.up * 0.9f;
            if (playerAnimator != null) playerAnimator.PlayCheer();
            BattleEffects.Sparkle(chest);
            Sfx.Play("wardrobe.equip", chest);

            if (item != null)
            {
                string group = ItemVoice.GroupFor(item);
                if (!string.IsNullOrEmpty(group)) VoicePlayer.Say(group);
            }
            if (Random.value < 0.5f) VoicePlayer.Say("wardrobe.pick");
        }

        void OnLockedChosen(RewardItem item)
        {
            if (!IsRunning || finishing) return;
            lastTapTime = Time.time;

            Sfx.Play("ui.locked");
            if (Time.time - lastLockedLineTime >= LockedLineGapSeconds)
            {
                lastLockedLineTime = Time.time;
                VoicePlayer.Say("wardrobe.locked");
            }
        }

        void OnDone()
        {
            if (!IsRunning || finishing) return;

            lastTapTime = Time.time;
            Sfx.Play("ui.pick");
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FinishRoutine());
        }

        // ----- Helpers ---------------------------------------------------------------------------

        void MovePlayer(Vector3 position, Quaternion rotation)
        {
            // Stand on the real ground under the point, in case the floor is a hair off.
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y - position.y) < 0.5f && !hit.transform.IsChildOf(player.transform))
                position.y = hit.point.y;

            player.Teleport(position, rotation);
        }
    }
}
