using System.Collections;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Voice
{
    /// <summary>
    /// Voice lines for the knights game that need more than a UnityEvent: a goblin giggles when
    /// it is bumped down (not the thief: it has its own "you got it back" line), a kid calls out when the dragon
    /// shows its fire warning, and the teacher explains the shield a few seconds after the first wave starts
    /// (once per game, and only when the child has not raised his shield yet).
    /// </summary>
    public class BattleVoice : MonoBehaviour
    {
        [SerializeField] KnightsGame game;
        [Tooltip("Seconds after the first wave starts before the shield hint is spoken.")]
        [SerializeField] float shieldHintDelaySeconds = 6f;

        // The hint waits for a quiet moment in the fight (not in a counting question), but never longer than this.
        const float MaxWaitForFightSeconds = 30f;

        BattleOpponentAI[] opponents;
        DragonBoss dragon;
        Coroutine hintRoutine;

        void Start()
        {
            if (game == null) game = FindAnyObjectByType<KnightsGame>();

            // Opponents and the dragon start switched off, so look for inactive ones too.
            opponents = FindObjectsByType<BattleOpponentAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (BattleOpponentAI opponent in opponents)
            {
                var hittable = opponent.GetComponent<Hittable>();
                if (hittable != null) hittable.Defeated += OnOpponentDefeated;
            }

            dragon = FindAnyObjectByType<DragonBoss>(FindObjectsInactive.Include);
            if (dragon != null) dragon.FireTelegraphStarted += OnFireTelegraph;

            if (game != null)
            {
                game.onWaveStarted.AddListener(OnWaveStarted);
                game.onEnded.AddListener(CancelHint);
            }
        }

        void OnDestroy()
        {
            if (opponents != null)
            {
                foreach (BattleOpponentAI opponent in opponents)
                {
                    if (opponent == null) continue;

                    var hittable = opponent.GetComponent<Hittable>();
                    if (hittable != null) hittable.Defeated -= OnOpponentDefeated;
                }
            }

            if (dragon != null) dragon.FireTelegraphStarted -= OnFireTelegraph;

            if (game != null)
            {
                game.onWaveStarted.RemoveListener(OnWaveStarted);
                game.onEnded.RemoveListener(CancelHint);
            }
        }

        void OnOpponentDefeated(Hittable who)
        {
            // The thief has its own line when the treasure comes back.
            var opponent = who != null ? who.GetComponent<BattleOpponentAI>() : null;
            if (opponent != null && opponent.IsThief) return;

            VoicePlayer.Say("knights.goblin_down");
        }

        void OnFireTelegraph()
        {
            VoicePlayer.Say("knights.fire_warning");
        }

        void OnWaveStarted(int wave)
        {
            if (wave != 1) return;

            CancelHint();
            hintRoutine = StartCoroutine(ShieldHintRoutine());
        }

        IEnumerator ShieldHintRoutine()
        {
            yield return new WaitForSeconds(shieldHintDelaySeconds);

            // Not in the middle of a counting question: wait until the goblins are down in the courtyard and he can fight.
            float giveUp = Time.time + MaxWaitForFightSeconds;
            while (game != null && game.IsRunning && !game.IsFighting && Time.time < giveUp)
                yield return null;

            hintRoutine = null;
            if (game != null && game.IsRunning && !game.PlayerHasBlocked) VoicePlayer.Say("knights.shield_hint");
        }

        void CancelHint()
        {
            if (hintRoutine == null) return;

            StopCoroutine(hintRoutine);
            hintRoutine = null;
        }
    }
}
