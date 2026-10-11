using CoolSchool.Audio;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Turns the one big button into a sword: tap to swing, hold to raise the shield.
    /// Sits on the player, switched off until the knights game turns it on.
    /// The player is never knocked out: when bumped he only wobbles for a moment.
    /// </summary>
    [RequireComponent(typeof(Hittable), typeof(SwordFighter))]
    public class PlayerBattleInput : MonoBehaviour
    {
        [SerializeField] ActionButton button;
        [Tooltip("Holding the button longer than this raises the shield instead of swinging.")]
        [SerializeField] float holdToBlockSeconds = 0.25f;

        Hittable hittable;
        SwordFighter fighter;
        PlayerController playerController;
        KidAnimator kidAnimator;

        bool wasHeld;
        bool isBlocking;
        float holdStartTime;

        void Awake()
        {
            hittable = GetComponent<Hittable>();
            fighter = GetComponent<SwordFighter>();
            playerController = GetComponent<PlayerController>();
            kidAnimator = GetComponent<KidAnimator>();
        }

        void OnEnable()
        {
            wasHeld = false;
            isBlocking = false;
            hittable.Hit += OnHit;
            hittable.Blocked += OnBlocked;
            hittable.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            if (hittable != null)
            {
                hittable.Hit -= OnHit;
                hittable.Blocked -= OnBlocked;
                hittable.Defeated -= OnDefeated;
            }

            StopBlocking();
            wasHeld = false;
            if (playerController != null) playerController.InputEnabled = true;
        }

        void Update()
        {
            // A bumped player stands still for a moment, then moves again.
            if (playerController != null)
                playerController.InputEnabled = !hittable.IsStunned;

            bool held = IsButtonHeld();
            if (held && !wasHeld)
            {
                holdStartTime = Time.time;
            }
            else if (held)
            {
                if (!isBlocking && Time.time - holdStartTime >= holdToBlockSeconds)
                {
                    isBlocking = true;
                    fighter.SetBlocking(true);
                }
            }
            else if (wasHeld)
            {
                // Let go: a short tap swings, a long hold just lowers the shield.
                if (isBlocking) StopBlocking();
                else fighter.Attack();
            }

            wasHeld = held;
        }

        bool IsButtonHeld()
        {
            if (button != null && button.IsHeld) return true;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.isPressed) return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.isPressed;
        }

        void StopBlocking()
        {
            if (!isBlocking) return;

            isBlocking = false;
            if (fighter != null) fighter.SetBlocking(false);
        }

        void OnHit(Hittable who)
        {
            if (kidAnimator != null) kidAnimator.PlayHit();
            Sfx.Play("player.bump", transform.position);
        }

        void OnBlocked(Hittable who)
        {
            if (kidAnimator != null) kidAnimator.PlayBlockHit();
        }

        /// <summary>The player must never be knocked out, so if this ever happens he just gets up again.</summary>
        void OnDefeated(Hittable who)
        {
            who.ResetState();
            who.IsBlocking = isBlocking;
        }
    }
}
