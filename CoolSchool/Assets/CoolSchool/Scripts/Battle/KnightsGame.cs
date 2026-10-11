using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Tag;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.Events;

namespace CoolSchool.Battle
{
    /// <summary>
    /// "Knights and the dragon". The player and three classmates become knights, two waves of
    /// goblins come out of the castle, then the friendly dragon walks in. After the dragon has
    /// been bumped enough times it flies away, everyone cheers, and it is back to free play.
    /// Everything that was changed is put back in OnEnd, so the game can be played again.
    /// </summary>
    public class KnightsGame : Activity
    {
        [Header("Player")]
        [SerializeField] PlayerController player;
        [SerializeField] PlayerBattleInput playerInput;
        [SerializeField] ActionButton actionButton;
        [SerializeField] Sprite swordIcon;
        [SerializeField] ImaginationEffect imagination;

        [Header("Goblins and trolls")]
        [SerializeField] Transform[] opponentSpawnPoints;
        [SerializeField] Transform retreatPoint;
        [Tooltip("The pool of goblins and trolls. They are reused for each wave.")]
        [SerializeField] BattleOpponentAI[] opponents;
        [Tooltip("How many goblins come out in each wave.")]
        [SerializeField] int[] waveSizes = { 2, 3 };

        [Header("Dragon")]
        [SerializeField] DragonBoss dragon;
        [SerializeField] Transform dragonEntry;
        [SerializeField] Transform arenaCenter;

        [Header("Knight classmates")]
        [SerializeField] KnightAllyAI[] allies;
        [SerializeField] WeaponHolder[] weaponHolders;

        [Header("Timing")]
        [SerializeField] float victorySeconds = 4f;

        [Header("Golden sword (won in the numbers lesson)")]
        [SerializeField] Color goldenSwordGlow = new Color(1.6f, 1.1f, 0.2f, 1f);

        [Header("Hooks for voice lines and sounds")]
        public UnityEvent<int> onWaveStarted;
        public UnityEvent onDragonArrived;
        public UnityEvent onVictory;

        const int GoldenBursts = 5;
        const float GoldenBurstGapSeconds = 0.3f;
        const float IntroSeconds = 1.5f;
        const float SpawnGapSeconds = 0.6f;
        const float WaveRestSeconds = 2.5f;
        const float WaitForFreeOpponentSeconds = 4f;
        // Safety net: if a wave somehow never finishes, send the rest home so the game goes on.
        const float WaveTimeoutSeconds = 120f;

        Coroutine flowRoutine;
        Coroutine goldenRoutine;
        bool dragonFlewAway;
        bool quitting;
        DragonBoss subscribedDragon;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            if (player != null) player.JumpEnabled = false;
            if (playerInput != null) playerInput.enabled = true;
            if (actionButton != null) actionButton.SetIcon(swordIcon);
            bool goldenSword = PowerUps.TryConsume(PowerUps.GoldenSword);
            if (goldenSword) SetGoldenSword(true);
            SetWeapons(true);
            if (goldenSword)
            {
                // Said before the normal start line (which comes right after OnBegin), so the voice queue plays them in order.
                VoicePlayer.Say("knights.golden_sword");
                goldenRoutine = StartCoroutine(GoldenSwordFanfare());
            }
            if (imagination != null) imagination.Play(true);
            Sfx.PlayMusic("music.knights");

            SetAlliesFighting(true);
            ResetHittables();
            DeactivateOpponentsAndDragon();

            flowRoutine = StartCoroutine(FlowRoutine());
        }

        protected override void OnEnd()
        {
            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
                flowRoutine = null;
            }
            UnsubscribeDragon();
            if (goldenRoutine != null)
            {
                StopCoroutine(goldenRoutine);
                goldenRoutine = null;
            }

            if (player != null) player.JumpEnabled = true;
            if (playerInput != null) playerInput.enabled = false;
            if (actionButton != null) actionButton.ResetIcon();
            SetWeapons(false);
            SetGoldenSword(false);

            SetAlliesFighting(false);
            DeactivateOpponentsAndDragon();
            ResetHittables();

            if (imagination != null) imagination.Play(false);
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        void OnDisable()
        {
            // If this object is switched off mid-game, put everything back first.
            if (IsRunning && !quitting) End();
        }

        // ---- The flow of the game ----

        IEnumerator FlowRoutine()
        {
            // Let the imagination glow play first.
            yield return new WaitForSeconds(IntroSeconds);

            if (waveSizes != null)
            {
                for (int wave = 0; wave < waveSizes.Length; wave++)
                    yield return RunWave(wave, waveSizes[wave]);
            }

            yield return RunDragon();

            // Hooray!
            CheerAll();
            BattleEffects.Confetti(PlayerPosition() + Vector3.up * 1.5f);
            onVictory?.Invoke();
            yield return new WaitForSeconds(victorySeconds);

            flowRoutine = null;
            End();
        }

        IEnumerator RunWave(int waveIndex, int count)
        {
            onWaveStarted?.Invoke(waveIndex + 1);
            Sfx.Play("battle.wave_horn");

            var wave = new List<BattleOpponentAI>();
            for (int i = 0; i < count; i++)
            {
                BattleOpponentAI opponent = PickOpponent(wave, true);
                float giveUpTime = Time.time + WaitForFreeOpponentSeconds;
                while (opponent == null && Time.time < giveUpTime)
                {
                    yield return null;
                    opponent = PickOpponent(wave, true);
                }
                // Nobody free: reuse one that is still running home.
                if (opponent == null) opponent = PickOpponent(wave, false);
                if (opponent == null) break;

                opponent.Activate(SpawnPosition(i), retreatPoint);
                wave.Add(opponent);
                yield return new WaitForSeconds(SpawnGapSeconds);
            }

            float timeout = Time.time + WaveTimeoutSeconds;
            while (!AllOut(wave) && Time.time < timeout)
                yield return null;

            foreach (BattleOpponentAI opponent in wave)
                if (!opponent.IsOut) opponent.gameObject.SetActive(false);

            yield return new WaitForSeconds(WaveRestSeconds);
        }

        IEnumerator RunDragon()
        {
            if (dragon == null) yield break;

            dragonFlewAway = false;
            subscribedDragon = dragon;
            dragon.Arrived += OnDragonArrived;
            dragon.FlewAway += OnDragonFlewAway;

            Vector3 center = arenaCenter != null ? arenaCenter.position : PlayerPosition();
            Vector3 entry = dragonEntry != null ? dragonEntry.position : center + Vector3.forward * 10f;
            dragon.Enter(entry, center);

            while (!dragonFlewAway)
                yield return null;

            UnsubscribeDragon();
        }

        void OnDragonArrived()
        {
            onDragonArrived?.Invoke();
        }

        void OnDragonFlewAway()
        {
            dragonFlewAway = true;
        }

        void UnsubscribeDragon()
        {
            if (subscribedDragon == null) return;

            subscribedDragon.Arrived -= OnDragonArrived;
            subscribedDragon.FlewAway -= OnDragonFlewAway;
            subscribedDragon = null;
        }

        // ---- Helpers ----

        /// <summary>Prefers opponents that are switched off, so nobody pops in mid-run.</summary>
        BattleOpponentAI PickOpponent(List<BattleOpponentAI> alreadyInWave, bool onlyInactive)
        {
            if (opponents == null) return null;

            foreach (BattleOpponentAI opponent in opponents)
            {
                if (opponent == null || alreadyInWave.Contains(opponent)) continue;
                if (onlyInactive && opponent.gameObject.activeSelf) continue;
                return opponent;
            }
            return null;
        }

        Vector3 SpawnPosition(int index)
        {
            if (opponentSpawnPoints != null && opponentSpawnPoints.Length > 0)
            {
                Transform point = opponentSpawnPoints[index % opponentSpawnPoints.Length];
                if (point != null) return point.position;
            }
            return arenaCenter != null ? arenaCenter.position : transform.position;
        }

        static bool AllOut(List<BattleOpponentAI> wave)
        {
            foreach (BattleOpponentAI opponent in wave)
                if (opponent != null && !opponent.IsOut) return false;
            return true;
        }

        Vector3 PlayerPosition()
        {
            return player != null ? player.transform.position : transform.position;
        }

        void SetWeapons(bool show)
        {
            if (weaponHolders == null) return;

            foreach (WeaponHolder holder in weaponHolders)
                if (holder != null) holder.ShowWeapons(show);
        }

        /// <summary>The golden sword: the player's sword glows gold and every swing counts double.</summary>
        void SetGoldenSword(bool on)
        {
            if (player == null) return;

            var fighter = player.GetComponent<SwordFighter>();
            if (fighter != null) fighter.HitMultiplier = on ? 2 : 1;

            var holder = player.GetComponent<WeaponHolder>();
            if (holder != null) holder.SetGlow(on ? goldenSwordGlow : (Color?)null);
        }

        /// <summary>Gold sparkles and confetti bursting off the player's new golden sword, so the child sees what he got.</summary>
        IEnumerator GoldenSwordFanfare()
        {
            var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            Sfx.Play("reward.item_reveal");

            for (int burst = 0; burst < GoldenBursts; burst++)
            {
                Vector3 spot = holder != null ? holder.SwordPosition : PlayerPosition() + Vector3.up * 1f;
                BattleEffects.Sparkle(spot);
                if (burst % 2 == 0) BattleEffects.Confetti(spot, new Color(1f, 0.82f, 0.2f));
                yield return new WaitForSeconds(GoldenBurstGapSeconds);
            }
            goldenRoutine = null;
        }

        /// <summary>Knights fight (their tag brain sleeps); afterwards they go back to playing tag.</summary>
        void SetAlliesFighting(bool fighting)
        {
            if (allies == null) return;

            foreach (KnightAllyAI ally in allies)
            {
                if (ally == null) continue;

                var tagBrain = ally.GetComponent<TagKidAI>();
                if (fighting)
                {
                    if (tagBrain != null) tagBrain.enabled = false;
                    ally.enabled = true;
                }
                else
                {
                    ally.enabled = false;
                    if (tagBrain != null) tagBrain.enabled = true;
                }
            }
        }

        /// <summary>Clears wobbles, shields and tumbles for everybody in the game.</summary>
        void ResetHittables()
        {
            ResetHittable(player);
            if (allies != null)
                foreach (KnightAllyAI ally in allies)
                    if (ally != null) ResetHittable(ally);

            if (opponents != null)
                foreach (BattleOpponentAI opponent in opponents)
                    if (opponent != null) ResetHittable(opponent);

            if (dragon != null) ResetHittable(dragon);
        }

        static void ResetHittable(Component owner)
        {
            if (owner == null) return;

            var hittable = owner.GetComponent<Hittable>();
            if (hittable != null) hittable.ResetState();
        }

        void DeactivateOpponentsAndDragon()
        {
            if (opponents != null)
            {
                foreach (BattleOpponentAI opponent in opponents)
                    if (opponent != null) opponent.gameObject.SetActive(false);
            }

            if (dragon != null) dragon.ResetDragon();
        }

        void CheerAll()
        {
            Cheer(player);
            if (allies == null) return;

            foreach (KnightAllyAI ally in allies)
                Cheer(ally);
        }

        static void Cheer(Component owner)
        {
            if (owner == null) return;

            var kid = owner.GetComponent<KidAnimator>();
            if (kid != null) kid.PlayCheer();
        }
    }
}
