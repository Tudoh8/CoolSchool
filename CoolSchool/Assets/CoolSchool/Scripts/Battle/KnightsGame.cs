using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Castle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Tag;
using CoolSchool.Voice;
using CoolSchool.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace CoolSchool.Battle
{
    /// <summary>
    /// "Knights and the castle adventure" (contract: Docs/specs/castle-adventure-contract.md). The player and three
    /// classmates become knights. At the gate a queen (a classmate) asks for help with a mission (guard the cake, the crown or
    /// the dragon egg, or rescue a knight from the tower), the child says the password, patches a hole in the wall, and
    /// fights two waves of silly goblins of different kinds (sometimes counting them on the wall first). Then the dragon
    /// arrives: it is hungry (or a sad mother looking for her egg), he helps it, they become friends and he rides on its back.
    /// Then everyone cheers at the gate, the castle grows, and it is back to free play. He can never fail.
    /// The flow is split over five files: this one (set up, putting everything back, shared helpers), KnightsGame.Visit.cs
    /// (the visit, the gate scene, the wall patch), KnightsGame.Waves.cs (goblins, counting, help a friend, the thief),
    /// KnightsGame.Dragon.cs (tower rescue, the dragon and the ride) and KnightsGame.Finale.cs (the celebration).
    /// Everything that was changed is put back in OnEnd, which can be called at any moment (the back button).
    /// </summary>
    public partial class KnightsGame : Activity
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
        [Tooltip("No longer used: the waves are built from the castle level table. Kept so old scenes still load.")]
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

        [Header("Castle adventure")]
        [Tooltip("The gate. The game holds it shut until the password is said.")]
        [SerializeField] CastlePortcullis portcullis;
        [Tooltip("In front of the gate, outside: where the child talks to the queen. He faces north.")]
        [SerializeField] Transform gateTalkSpot;
        [Tooltip("Where the queen walks to inside the courtyard once the gate is open. Optional: a spot near the wall patch is used when empty.")]
        [SerializeField] Transform queenSpot;
        [Tooltip("7 places on top of the back wall where goblins stand to be counted.")]
        [SerializeField] Transform[] wallPerches;
        [Tooltip("7 matching landing points on the courtyard floor (NavMesh).")]
        [SerializeField] Transform[] perchLandings;
        [Tooltip("The 2 barrels where the tiny goblin hides.")]
        [SerializeField] CastleBarrel[] barrels;
        [Tooltip("8 points around the courtyard the thief runs between.")]
        [SerializeField] Transform[] thiefRunPoints;
        [SerializeField] MissionLoot cakeLoot;
        [SerializeField] MissionLoot crownLoot;
        [SerializeField] MissionLoot eggLoot;
        [Tooltip("Where the child hops down after the dragon ride.")]
        [SerializeField] Transform rideDismountPoint;
        [Tooltip("The picture on the action button during the dragon ride.")]
        [SerializeField] Sprite fireIcon;
        [SerializeField] LessonUI lessonUI;
        [SerializeField] LetterSet letters;

        [Header("Castle moments")]
        [SerializeField] CoolSchool.Castle.CastleQueen queen;
        [SerializeField] CoolSchool.Castle.CastleMissionProps missionProps;
        [SerializeField] CoolSchool.Castle.CastleGuideMarker guide;
        [SerializeField] CoolSchool.Castle.PasswordGate password;
        [SerializeField] CoolSchool.Castle.WallPatch wall;
        [SerializeField] CoolSchool.Castle.CookiePlate cookies;
        [SerializeField] CoolSchool.Castle.DragonEgg egg;
        [SerializeField] CoolSchool.Castle.DragonNaming naming;
        [SerializeField] CoolSchool.Castle.BabyDragon babyDragon;
        [SerializeField] CoolSchool.Castle.DragonRidePath ridePath;
        [SerializeField] CoolSchool.Castle.NameFlag nameFlag;
        [SerializeField] CoolSchool.Castle.CastleGrowth growth;
        [SerializeField] CoolSchool.Castle.TowerRescue towerRescue;
        [SerializeField] Transform queenCelebrateSpot;
        [SerializeField] Transform babyCelebrateSpot;

        [Header("Hooks for voice lines and sounds")]
        public UnityEvent<int> onWaveStarted;
        public UnityEvent onDragonArrived;
        public UnityEvent onVictory;

        const int GoldenBursts = 5;
        const float GoldenBurstGapSeconds = 0.3f;
        const float IntroSeconds = 1.5f;
        // Running to a spot: how fast the player and the knights run, and how long they get to get there.
        const float PlayerRunSpeed = 4f;
        const float AllyRunSpeed = 5f;
        const float GatherGiveUpSeconds = 8f;
        // How close counts as "there" (metres).
        const float ArriveDistance = 0.25f;
        const float MaxVoiceWaitSeconds = 8f;
        // Allies stand in a little arc behind the player (further from the camera): sideways and backwards offsets.
        static readonly Vector2[] AllyArc = { new Vector2(-1.7f, 0.8f), new Vector2(1.7f, 0.8f), new Vector2(0f, 1.8f) };

        /// <summary>What the child can do right now: stand still, only walk, or walk and fight (sword and shield).</summary>
        enum PlayerMode
        {
            Frozen,
            Walk,
            Fight
        }

        Coroutine flowRoutine;
        Coroutine goldenRoutine;
        bool goldenSwordUsed;
        bool finished;
        bool quitting;
        bool hudHidden;
        bool fightingNow;
        bool playerHasBlocked;
        Hittable playerHittable;
        DragonBoss subscribedDragon;
        bool dragonArrived;
        // The running-to-the-gate walks, so OnEnd can stop them, and the speeds of the allies' agents to put back.
        readonly List<Coroutine> walkRoutines = new List<Coroutine>();
        readonly Dictionary<NavMeshAgent, float> originalSpeeds = new Dictionary<NavMeshAgent, float>();
        int walkersBusy;

        /// <summary>This visit's mission (chosen at the start of the visit).</summary>
        public CastleMission Mission => mission;

        /// <summary>The castle level (1 to 3) at the start of this visit.</summary>
        public int Level => level;

        /// <summary>True while the child may fight (goblins are in the courtyard and no question is up).</summary>
        public bool IsFighting => IsRunning && fightingNow;

        /// <summary>True once the child has raised his shield in this visit.</summary>
        public bool PlayerHasBlocked => playerHasBlocked;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            playerHittable = player != null ? player.GetComponent<Hittable>() : null;

            BattleOpponentAI.BeginVisit();
            finished = false;
            hudHidden = false;
            fightingNow = false;
            playerHasBlocked = false;
            results.Clear();
            babyHatched = false;

            if (player != null) player.JumpEnabled = false;
            SetPlayerMode(PlayerMode.Frozen);
            if (actionButton != null) actionButton.SetIcon(swordIcon);
            bool goldenSword = PowerUps.TryConsume(PowerUps.GoldenSword);
            goldenSwordUsed = goldenSword;
            if (goldenSword) SetGoldenSword(true);
            SetWeapons(true);
            if (goldenSword)
            {
                VoicePlayer.Say("knights.golden_sword");
                goldenRoutine = StartCoroutine(GoldenSwordFanfare());
            }
            if (imagination != null) imagination.Play(true);
            Sfx.PlayMusic("music.knights");

            // The knights are in fighting mode, but their brain stays off until the first wave.
            SetAlliesFighting(true);
            ResetHittables();
            DeactivateOpponentsAndDragon();
            ResetCastleMoments();

            if (portcullis != null) portcullis.HoldClosed = true;
            PickVisit();
            SetUpMission();

            flowRoutine = StartCoroutine(FlowRoutine());
        }

        protected override void OnEnd()
        {
            StopAllCoroutines();
            flowRoutine = null;
            goldenRoutine = null;
            helpRoutine = null;
            UnsubscribeDragon();
            if (actionButton != null) actionButton.Pressed -= OnRidePressed;

            // Stopped mid-run to the gate: hand the player and the allies' agents back as they were.
            StopWalks();
            RestoreAllyAgents();

            if (player != null)
            {
                // Stopped in the middle of the dragon ride: hop off and stand up.
                if (player.IsRiding) player.StopRiding(rideDismountPoint != null ? rideDismountPoint.position : PlayerPosition());
                var kid = player.GetComponent<KidAnimator>();
                if (kid != null) kid.SetSitting(false);

                player.InputEnabled = true;
                player.JumpEnabled = true;
            }
            if (playerInput != null) playerInput.enabled = false;
            fightingNow = false;
            if (lessonUI != null)
            {
                lessonUI.StopPulse();
                lessonUI.HideCards();
                if (hudHidden) lessonUI.SetGameplayHudVisible(true);
            }
            hudHidden = false;
            if (actionButton != null) actionButton.ResetIcon();
            SetWeapons(false);
            SetGoldenSword(false);

            if (portcullis != null) portcullis.HoldClosed = false;
            ResetCastleMoments();
            ResetBarrels();
            ClearHelpMarkers();
            stuckKnight = null;

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

        void Update()
        {
            // Remember that he has found the shield, so the teacher's hint does not nag.
            if (IsRunning && !playerHasBlocked && playerHittable != null && playerHittable.IsBlocking)
                playerHasBlocked = true;
        }

        // ---- The flow of the game ----

        IEnumerator FlowRoutine()
        {
            // Let the imagination glow play first.
            yield return new WaitForSeconds(IntroSeconds);

            yield return RunGateScene();
            yield return RunCourtyard();
            yield return RunWaves();
            if (mission == CastleMission.Knight) yield return RunTowerRescue();
            yield return RunDragonVisit();
            yield return RunDragonRide();
            yield return RunFinale();

            flowRoutine = null;
            End();
        }

        // ---- The child's controls ----

        /// <summary>
        /// Frozen: no walking, no sword. Walk: the joystick works, the button does nothing. Fight: sword, shield and walking.
        /// (PlayerBattleInput gives the control back when it is switched off, so Frozen turns it off first and then freezes.)
        /// </summary>
        void SetPlayerMode(PlayerMode mode)
        {
            fightingNow = mode == PlayerMode.Fight;
            if (playerInput != null) playerInput.enabled = mode == PlayerMode.Fight;
            if (player != null && mode != PlayerMode.Fight) player.InputEnabled = mode == PlayerMode.Walk;
        }

        // ---- Talking ----

        /// <summary>Waits for whoever is talking (and whoever is waiting to talk) to finish, but never for long.</summary>
        IEnumerator WaitForVoice(float maxSeconds = MaxVoiceWaitSeconds)
        {
            yield return new WaitForSeconds(0.3f);

            float giveUp = Time.time + maxSeconds;
            while (VoicePlayer.Instance != null && VoicePlayer.Instance.IsBusy && Time.time < giveUp)
                yield return null;
        }

        /// <summary>Says a line when nobody is talking and waits until it is done (a kid never talks over the teacher).</summary>
        IEnumerator SayAndWait(string group)
        {
            yield return WaitForVoice();
            VoicePlayer.Say(group);
            yield return WaitForVoice();
        }

        /// <summary>
        /// Runs another routine, but stops waiting for it after "seconds", so a moment that got stuck can never hold the
        /// whole visit (the moments have their own, shorter safety nets).
        /// </summary>
        IEnumerator Bounded(IEnumerator inner, float seconds)
        {
            if (inner == null) yield break;

            float end = Time.time + seconds;
            while (Time.time < end && inner.MoveNext())
                yield return inner.Current;
        }

        void SetHud(bool visible)
        {
            if (lessonUI == null) return;

            lessonUI.SetGameplayHudVisible(visible);
            hudHidden = !visible;
        }

        // ---- Walking ----

        /// <summary>
        /// Runs the player to a point by hand (his controls are off), then turns him to face a direction. If he is stuck
        /// he is lifted to the spot afterwards.
        /// </summary>
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

        /// <summary>Walks the player to a spot, and lifts him there if the walk did not get him close.</summary>
        IEnumerator MovePlayerTo(Vector3 point, Vector3 face)
        {
            if (player == null) yield break;

            yield return WalkPlayerTo(point, face);

            Vector3 away = point - player.transform.position;
            away.y = 0f;
            if (away.magnitude > ArriveDistance * 4f)
            {
                face.y = 0f;
                Quaternion facing = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face, Vector3.up) : player.transform.rotation;
                player.Teleport(point, facing);
            }
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
            subscribedDragon = null;
        }

        void OnDragonArrived()
        {
            dragonArrived = true;
            onDragonArrived?.Invoke();
        }

        // ---- Helpers ----

        Vector3 PlayerPosition()
        {
            return player != null ? player.transform.position : transform.position;
        }

        Vector3 ArenaCenter()
        {
            return arenaCenter != null ? arenaCenter.position : PlayerPosition();
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
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

        /// <summary>
        /// Knights fight (their tag brain sleeps); afterwards they go back to playing tag. Their fighting brain stays off
        /// until EnableAllies(true) at the first wave.
        /// </summary>
        void SetAlliesFighting(bool fighting)
        {
            if (allies == null) return;

            foreach (KnightAllyAI ally in allies)
            {
                if (ally == null) continue;

                var tagBrain = ally.GetComponent<TagKidAI>();
                ally.enabled = false;
                if (tagBrain != null) tagBrain.enabled = !fighting;
            }
        }

        /// <summary>Switches the knights' fighting brains on or off (the knight stuck on the tower stays out of it).</summary>
        void EnableAllies(bool on)
        {
            if (allies == null) return;

            foreach (KnightAllyAI ally in allies)
            {
                if (ally == null || ally == stuckKnight) continue;

                ally.enabled = on;
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
            if (allies != null)
            {
                foreach (KnightAllyAI ally in allies)
                    Cheer(ally);
            }
            if (queen != null) queen.Cheer();
        }

        static void Cheer(Component owner)
        {
            if (owner == null) return;

            var kid = owner.GetComponent<KidAnimator>();
            if (kid != null) kid.PlayCheer();
        }
    }
}
