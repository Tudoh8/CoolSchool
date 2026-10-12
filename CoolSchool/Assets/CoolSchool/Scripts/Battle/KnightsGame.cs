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
using UnityEngine.AI;
using UnityEngine.Events;

namespace CoolSchool.Battle
{
    /// <summary>
    /// "Knights and the dragon". The player and three classmates become knights, two waves of
    /// goblins climb over the castle wall on ladders, then the friendly dragon walks in. After the dragon has
    /// been bumped enough times it flies away. Then everyone runs to the castle gate, cheers facing the camera,
    /// and it is back to free play.
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
        [Tooltip("The ladders up the outside of the back wall. Goblins climb them, jump down into the courtyard and run home to them.")]
        [SerializeField] WallLadder[] ladders;
        [Tooltip("The pool of goblins and trolls. They are reused for each wave.")]
        [SerializeField] BattleOpponentAI[] opponents;
        [Tooltip("How many goblins come out in each wave.")]
        [SerializeField] int[] waveSizes = { 2, 3 };

        [Header("Dragon")]
        [SerializeField] DragonBoss dragon;
        [SerializeField] Transform dragonEntry;
        [SerializeField] Transform arenaCenter;
        [Tooltip("The dragon swoops in through the air and lands. Off: it walks in along the ground.")]
        [SerializeField] bool dragonFliesIn = true;

        [Header("Knight classmates")]
        [SerializeField] KnightAllyAI[] allies;
        [SerializeField] WeaponHolder[] weaponHolders;

        [Header("Timing")]
        [SerializeField] float victorySeconds = 4f;

        [Header("Victory")]
        [Tooltip("Where everyone runs to cheer after the dragon flew away, just inside the gate. Empty: they cheer where they stand.")]
        [SerializeField] Transform celebrateSpot;

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
        // How long goblins that were sent home get to run to their ladder and vanish.
        const float SendHomeSeconds = 6f;
        // Safety net: if a wave somehow never finishes, send the rest home so the game goes on.
        const float WaveTimeoutSeconds = 120f;
        // Running to the gate to cheer: how fast the player and the knights run, and how long they get to get there.
        const float PlayerRunSpeed = 4f;
        const float AllyRunSpeed = 5f;
        const float GatherGiveUpSeconds = 8f;
        // How close counts as "there" (metres).
        const float ArriveDistance = 0.25f;
        // Allies stand in a little arc behind the player (further from the camera): sideways and backwards offsets.
        static readonly Vector2[] AllyArc = { new Vector2(-1.7f, 0.8f), new Vector2(1.7f, 0.8f), new Vector2(0f, 1.8f) };

        Coroutine flowRoutine;
        Coroutine goldenRoutine;
        bool dragonFlewAway;
        bool goldenSwordUsed;
        bool finished;
        bool quitting;
        DragonBoss subscribedDragon;
        // The running-to-the-gate walks, so OnEnd can stop them, and the speeds of the allies' agents to put back.
        readonly List<Coroutine> walkRoutines = new List<Coroutine>();
        readonly Dictionary<NavMeshAgent, float> originalSpeeds = new Dictionary<NavMeshAgent, float>();
        int walkersBusy;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            if (player != null) player.JumpEnabled = false;
            if (playerInput != null) playerInput.enabled = true;
            if (actionButton != null) actionButton.SetIcon(swordIcon);
            bool goldenSword = PowerUps.TryConsume(PowerUps.GoldenSword);
            goldenSwordUsed = goldenSword;
            finished = false;
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

            // Stopped mid-run to the gate: hand the player and the allies' agents back as they were.
            StopWalks();
            RestoreAllyAgents();

            if (player != null)
            {
                player.InputEnabled = true;
                player.JumpEnabled = true;
            }
            if (playerInput != null) playerInput.enabled = false;
            if (actionButton != null) actionButton.ResetIcon();
            SetWeapons(false);
            SetGoldenSword(false);

            SetAlliesFighting(false);
            DeactivateOpponentsAndDragon();
            ResetHittables();

            if (imagination != null) imagination.Play(false);

            // Stopped early (the back button): the golden sword was not really used, so he keeps it for the next time.
            if (goldenSwordUsed && !finished) PowerUps.Restore(PowerUps.GoldenSword);
            goldenSwordUsed = false;
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

            // Hooray! Everyone runs to the gate first, so the cheer happens right in front of the camera.
            finished = true;
            yield return GatherAtGate();
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
            // Each wave starts on another ladder, so the goblins don't always climb the same ones.
            int firstLadder = ladders != null && ladders.Length > 0 ? Random.Range(0, ladders.Length) : 0;
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

                // The goblin starts behind the wall at the ladder's foot; the sparkle comes when it pops up over the wall.
                opponent.Activate(LadderFor(firstLadder + i), arenaCenter != null ? arenaCenter.position : PlayerPosition());
                wave.Add(opponent);
                yield return new WaitForSeconds(SpawnGapSeconds);
            }

            float timeout = Time.time + WaveTimeoutSeconds;
            while (!AllOut(wave) && Time.time < timeout)
                yield return null;

            // Anyone left (the wave dragged on) runs home to a ladder and vanishes in a sparkle.
            foreach (BattleOpponentAI opponent in wave)
                if (!opponent.IsOut) opponent.SendHome();

            float sendHomeTimeout = Time.time + SendHomeSeconds;
            while (!AllOut(wave) && Time.time < sendHomeTimeout)
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
            if (dragonFliesIn) dragon.FlyIn(entry, center);
            else dragon.Enter(entry, center);

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

        /// <summary>
        /// After the dragon flew away the player and the knights run to the celebrate spot just inside the gate and turn to face
        /// the camera. The player runs by hand (his controls are off), the knights by the navigation. If the player is stuck
        /// he is lifted to the spot. Without a celebrate spot nobody moves.
        /// </summary>
        IEnumerator GatherAtGate()
        {
            if (celebrateSpot == null) yield break;

            Vector3 spot = celebrateSpot.position;
            Vector3 faceCamera = Vector3.back;

            // Nobody wanders off: the player's controls and the knights' fighting brain stay off (the tag brain is off already).
            if (player != null) player.InputEnabled = false;
            if (allies != null)
                foreach (KnightAllyAI ally in allies)
                    if (ally != null) ally.enabled = false;

            walkersBusy = 0;
            if (player != null) walkRoutines.Add(StartCoroutine(WalkPlayerTo(spot, faceCamera)));
            if (allies != null)
            {
                int slot = 0;
                foreach (KnightAllyAI ally in allies)
                {
                    if (ally == null || !ally.gameObject.activeInHierarchy) continue;

                    walkRoutines.Add(StartCoroutine(WalkAllyTo(ally, AllySpot(spot, slot++), faceCamera)));
                }
            }

            // The walks give up by themselves; the extra second is for turning round at the end.
            float giveUp = Time.time + GatherGiveUpSeconds + 1f;
            while (walkersBusy > 0 && Time.time < giveUp) yield return null;
            StopWalks();

            if (player != null)
            {
                Vector3 away = spot - player.transform.position;
                away.y = 0f;
                if (away.magnitude > ArriveDistance * 2f) player.Teleport(spot, Quaternion.LookRotation(faceCamera, Vector3.up));
            }
        }

        /// <summary>
        /// Where a knight stands around the player: the first three in a little arc behind him, the extra ones continue
        /// outwards on both sides. "Behind" is away from the camera (larger z).
        /// </summary>
        static Vector3 AllySpot(Vector3 playerSpot, int index)
        {
            if (index < AllyArc.Length) return playerSpot + new Vector3(AllyArc[index].x, 0f, AllyArc[index].y);

            int extra = index - AllyArc.Length;
            float side = extra % 2 == 0 ? -1f : 1f;
            return playerSpot + new Vector3(side * (3.2f + 1.5f * (extra / 2)), 0f, 1.6f);
        }

        /// <summary>Runs the player to a point by hand (his controls are off), then turns him to face a direction.</summary>
        IEnumerator WalkPlayerTo(Vector3 point, Vector3 face)
        {
            var controller = player.GetComponent<CharacterController>();

            walkersBusy++;
            float giveUp = Time.time + GatherGiveUpSeconds;
            while (controller != null && controller.enabled && Time.time < giveUp)
            {
                Vector3 toPoint = point - player.transform.position;
                toPoint.y = 0f;
                float distance = toPoint.magnitude;
                if (distance < ArriveDistance) break;

                controller.Move(toPoint / distance * Mathf.Min(PlayerRunSpeed * Time.deltaTime, distance));
                player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(toPoint), 720f * Time.deltaTime);
                yield return null;
            }

            yield return TurnToFace(player.transform, face, 720f);
            walkersBusy--;
        }

        /// <summary>Sends a knight (by the navigation) to a point and turns him to face a direction.</summary>
        IEnumerator WalkAllyTo(KnightAllyAI ally, Vector3 point, Vector3 face)
        {
            walkersBusy++;
            var agent = ally.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) point = hit.position;
                if (!originalSpeeds.ContainsKey(agent)) originalSpeeds[agent] = agent.speed;
                agent.speed = AllyRunSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.SetDestination(point);

                float giveUp = Time.time + GatherGiveUpSeconds;
                yield return null;
                while (Time.time < giveUp && (agent.pathPending || agent.remainingDistance > ArriveDistance))
                    yield return null;
                agent.isStopped = true;
            }

            yield return TurnToFace(ally.transform, face, 540f);
            walkersBusy--;
        }

        static IEnumerator TurnToFace(Transform who, Vector3 face, float degreesPerSecond)
        {
            face.y = 0f;
            if (face.sqrMagnitude < 0.01f) yield break;

            Quaternion wanted = Quaternion.LookRotation(face.normalized, Vector3.up);
            for (float elapsed = 0f; elapsed < 0.4f; elapsed += Time.deltaTime)
            {
                who.rotation = Quaternion.RotateTowards(who.rotation, wanted, degreesPerSecond * Time.deltaTime);
                yield return null;
            }
        }

        void StopWalks()
        {
            foreach (Coroutine walk in walkRoutines)
                if (walk != null) StopCoroutine(walk);
            walkRoutines.Clear();
            walkersBusy = 0;
        }

        /// <summary>Gives the knights' agents their old speed back and lets them move again (the tag brain takes over).</summary>
        void RestoreAllyAgents()
        {
            foreach (KeyValuePair<NavMeshAgent, float> saved in originalSpeeds)
            {
                NavMeshAgent agent = saved.Key;
                if (agent == null) continue;

                agent.speed = saved.Value;
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = false;
            }
            originalSpeeds.Clear();
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

        /// <summary>The ladder for the n-th goblin of a wave, or null when there are none (the goblin then just appears).</summary>
        WallLadder LadderFor(int index)
        {
            if (ladders == null || ladders.Length == 0) return null;

            return ladders[index % ladders.Length];
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
