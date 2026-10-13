using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.Events;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// "De dieren zijn ontsnapt!" Hide and seek in the zoo park. The zookeeper runs in: the zoo animals have escaped. He counts with
    /// his eyes closed (the overlay shows the digits and he counts along), then searches the hiding spots; the animals peek, giggle
    /// and call, and hints get stronger when he is stuck (first sound, position word, a shaking cover, a sparkle trail), so he
    /// can never be stuck. Every animal found joins a conga line and is ticked off a checklist of silhouettes. Then it is his turn
    /// to hide (the keeper counts, then searches wrong spots first and always finds him), the animals go home to their pens one by
    /// one with "where were they?" in position words, a "how many?" question and a party. The zoo fills up over the visits.
    /// The flow is split over four files: this one (set up, choosing the hiders, levels, putting everything back),
    /// HideAndSeekGame.Seek.cs (the count, checking spots, hints, the "how many are left" question), HideAndSeekGame.HideTurn.cs
    /// (his turn to hide) and HideAndSeekGame.Recap.cs (the pens, the "how many" question, the party). Contract:
    /// Docs/specs/hide-and-seek-contract.md. Everything that was changed is put back in OnEnd, also when ended at any moment.
    /// </summary>
    public partial class HideAndSeekGame : Activity
    {
        [Header("Player")]
        [SerializeField] PlayerController player;
        [SerializeField] KidAnimator playerAnimator;

        [Header("Park")]
        [SerializeField] ZooKeeper keeper;
        [Tooltip("The 10 animals, in ZooAnimalKind.Order.")]
        [SerializeField] ZooAnimal[] animals;
        [Tooltip("The 10 icons, in ZooAnimalKind.Order (made by the icon renderer).")]
        [SerializeField] Sprite[] icons;
        [Tooltip("The 10 pens, in ZooAnimalKind.Order.")]
        [SerializeField] ZooPen[] pens;
        [SerializeField] HidingSpot[] spots;           // 12
        [SerializeField] SurpriseCritter[] critters;   // 3
        [SerializeField] CongaLine conga;
        [SerializeField] SparkleTrail trail;
        [Tooltip("In front of the pens, facing north. He walks here with the animals to take them home.")]
        [SerializeField] Transform penWalkPoint;
        [SerializeField] Transform[] partyPoints;      // 6
        [Tooltip("In front of the base tree, where the keeper counts.")]
        [SerializeField] Transform countPoint;

        [Header("UI")]
        [SerializeField] ZooChecklistUI checklist;
        [SerializeField] EyesClosedOverlay eyes;
        [SerializeField] LessonUI lessonUI;

        [Header("Tuning")]
        [SerializeField] float countStepSeconds = 0.9f;
        [Tooltip("A hidden animal within this many metres of the child giggles or calls every 4 to 6 seconds.")]
        [SerializeField] float callRadius = 8f;
        [Tooltip("1 to 3 plays that level whatever is saved (0 = off).")]
        [SerializeField] int debugForceLevel = 0;
        [Tooltip("After the count, every animal is found at once (to test his turn to hide and the way home).")]
        [SerializeField] bool debugSkipToHideTurn;

        [Header("Hooks for voice lines and sounds")]
        public UnityEvent onVictory;

        /// <summary>Raised at the party: the stars to give (animals found + 1) and where the child stands.</summary>
        public event Action<int, Vector3> RoundFinished;

        // The adaptive level: 1 to 3, read when the round starts.
        const string Skill = "hideseek";
        const int MaxLevel = 3;
        static readonly int[] HidersByLevel = { 3, 4, 5 };
        static readonly int[] SeekCountByLevel = { 5, 10, 10 };
        static readonly int[] HideCountCapByLevel = { 10, 15, 20 };
        static readonly float[] PeekMinByLevel = { 4f, 6f, 8f };
        static readonly float[] PeekMaxByLevel = { 6f, 9f, 12f };
        static readonly float[] HintOneByLevel = { 8f, 12f, 18f };
        static readonly float[] HintTwoByLevel = { 16f, 24f, 32f };
        static readonly float[] HintThreeByLevel = { 26f, 36f, 45f };
        static readonly int[] DecoysByLevel = { 2, 2, 3 };
        static readonly int[] CardsByLevel = { 2, 3, 3 };

        const float MaxVoiceWaitSeconds = 8f;
        const float MinSpotDistanceFromPlayer = 7f;
        const float SpotsApartMeters = 6f;
        const int MaxSearchSteps = 4000;

        /// <summary>One animal that hides this round: where, whether it is found, and its slot on the checklist.</summary>
        sealed class Hider
        {
            public ZooAnimalKind kind;
            public ZooAnimal animal;
            public ZooPen pen;
            public HidingSpot spot;
            public int slot;
            public bool found;
            public bool wasKnown;
            public float nextPeek;
            public float nextCall;
        }

        // What the child has done this round.
        int level = 1;
        int mistakes;
        bool roundCounted;
        bool quitting;
        bool hudHidden;
        bool playerDancing;
        Coroutine flowRoutine;
        readonly List<Hider> hiders = new List<Hider>();
        readonly List<Hider> foundOrder = new List<Hider>();
        // The kinds of the last round, so the same set is not played twice in a row (not saved).
        string lastSetKey = string.Empty;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (playerAnimator == null && player != null) playerAnimator = player.GetComponent<KidAnimator>();
            if (player == null)
            {
                End();
                return;
            }

            level = Mathf.Clamp(debugForceLevel > 0 ? debugForceLevel : ProgressStore.Current.GetLevel(Skill), 1, MaxLevel);
            mistakes = 0;
            roundCounted = false;
            hiders.Clear();
            foundOrder.Clear();
            ResetSeekState();
            ResetWorld();

            flowRoutine = StartCoroutine(FlowRoutine());
        }

        protected override void OnEnd()
        {
            StopAllCoroutines();
            flowRoutine = null;
            hintRoutine = null;

            if (trail != null) trail.Stop();
            if (eyes != null && eyes.IsClosed) eyes.Open();
            if (checklist != null) checklist.Hide();
            if (lessonUI != null)
            {
                lessonUI.StopPulse();
                lessonUI.HideCards();
                if (hudHidden) lessonUI.SetGameplayHudVisible(true);
            }
            hudHidden = false;

            RestorePlayer();

            // Found animals count, also when he stops in the middle: a pen once filled stays filled.
            CommitProgress(false);

            if (conga != null) conga.Clear();
            if (keeper != null) keeper.GoHome();
            PutAnimalsBack();
            ResetWorld();
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

        // ----- The flow of the game ----------------------------------------------------------------

        IEnumerator FlowRoutine()
        {
            yield return RunBegin();
            yield return RunCount();
            yield return RunSeek();
            yield return RunHideTurn();
            yield return RunBackHome();
            yield return RunHowManyFound();
            yield return RunParty();

            flowRoutine = null;
            End();
        }

        /// <summary>The keeper runs in, the hiders are chosen and escape from their pens, the checklist shows their silhouettes.</summary>
        IEnumerator RunBegin()
        {
            Sfx.PlayMusic("music.zoo");
            // He stands still from now on: the hiding spots are chosen (7 m rule) for the place where he really counts.
            FreezePlayer(true);

            if (keeper != null)
            {
                keeper.AppearAtHome();
                keeper.Follow(player.transform, 2.5f);
            }
            if (conga != null)
            {
                conga.Clear();
                conga.Leader = player.transform;
            }

            ChooseHiders();
            if (hiders.Count == 0 || spots == null || spots.Length == 0)
            {
                // Nothing is set up: nothing to play, end quietly.
                flowRoutine = null;
                End();
                yield break;
            }

            Say("zoo.start", true);
            yield return EscapeFromPens();

            Say("zoo.escaped");
            if (checklist != null)
            {
                var pictures = new Sprite[hiders.Count];
                for (int i = 0; i < pictures.Length; i++) pictures[i] = IconOf(hiders[i].kind);
                checklist.Show(pictures);
            }
            yield return WaitForVoice();
        }

        /// <summary>Each hider that lives in a pen: the gate swings open, it hops, there is a puff, and it is gone.</summary>
        IEnumerator EscapeFromPens()
        {
            var leaving = new List<Hider>();
            foreach (Hider hider in hiders)
            {
                if (hider.wasKnown && hider.pen != null && hider.animal != null) leaving.Add(hider);
                else if (hider.animal != null) hider.animal.Hide();
            }
            if (leaving.Count == 0)
            {
                yield return new WaitForSeconds(0.5f);
                yield break;
            }

            // The pens are placed in free play already, but make sure the animals really stand in them.
            foreach (Hider hider in leaving)
            {
                if (!hider.animal.IsVisible) hider.pen.PlaceInstantly(hider.animal);
                hider.pen.OpenGate(true);
            }
            yield return new WaitForSeconds(0.45f);

            foreach (Hider hider in leaving) hider.animal.HappyHop();
            yield return new WaitForSeconds(0.4f);

            foreach (Hider hider in leaving)
            {
                BattleEffects.Sparkle(hider.animal.transform.position + Vector3.up * (hider.animal.Height * 0.5f));
                hider.animal.Hide();
                hider.pen.OpenGate(false);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // ----- Choosing the hiders -----------------------------------------------------------------

        /// <summary>
        /// The new kinds first (all of them on the very first visit, otherwise one, or as many as it takes to fill the round when
        /// fewer are known), then known kinds at random, never the same set twice in a row if it can be avoided. Then the spots.
        /// </summary>
        void ChooseHiders()
        {
            hiders.Clear();
            int wanted = ByLevel(HidersByLevel);

            var known = new List<ZooAnimalKind>();
            var fresh = new List<ZooAnimalKind>();
            foreach (ZooAnimalKind kind in ZooAnimalKind.Order)
                (ZooProgress.IsKnown(kind.Id) ? known : fresh).Add(kind);

            int newCount;
            if (known.Count == 0) newCount = wanted;
            else if (known.Count >= wanted - 1) newCount = 1;
            else newCount = wanted - known.Count;
            newCount = Mathf.Min(newCount, fresh.Count);

            var chosen = new List<ZooAnimalKind>();
            for (int i = 0; i < newCount; i++) chosen.Add(fresh[i]);

            int fill = wanted - chosen.Count;
            if (fill > 0 && known.Count > 0)
            {
                List<ZooAnimalKind> best = null;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var pool = new List<ZooAnimalKind>(known);
                    Shuffle(pool);
                    List<ZooAnimalKind> candidate = pool.GetRange(0, Mathf.Min(fill, pool.Count));
                    best = candidate;
                    var trial = new List<ZooAnimalKind>(chosen);
                    trial.AddRange(candidate);
                    if (SetKey(trial) != lastSetKey) break;
                }
                if (best != null) chosen.AddRange(best);
            }

            // Not enough known kinds to fill the round: any other kind will do.
            foreach (ZooAnimalKind kind in ZooAnimalKind.Order)
            {
                if (chosen.Count >= wanted) break;
                if (!chosen.Contains(kind)) chosen.Add(kind);
            }

            lastSetKey = SetKey(chosen);
            for (int i = 0; i < chosen.Count; i++)
            {
                ZooAnimalKind kind = chosen[i];
                hiders.Add(new Hider
                {
                    kind = kind,
                    animal = AnimalOf(kind),
                    pen = PenOf(kind),
                    slot = i,
                    wasKnown = ZooProgress.IsKnown(kind.Id),
                });
            }

            // The animals that cannot be shown (not wired) cannot hide.
            hiders.RemoveAll(h => h.animal == null);
            for (int i = 0; i < hiders.Count; i++) hiders[i].slot = i;

            AssignSpots();
            hiders.RemoveAll(h => h.spot == null);
            for (int i = 0; i < hiders.Count; i++) hiders[i].slot = i;
        }

        static string SetKey(List<ZooAnimalKind> kinds)
        {
            var ids = new List<string>();
            foreach (ZooAnimalKind kind in kinds) ids.Add(kind.Id);
            ids.Sort(StringComparer.Ordinal);
            return string.Join(",", ids);
        }

        // Backtracking state for AssignSpots.
        List<HidingSpot>[] spotOptions;
        HidingSpot[] spotChoice;
        int[] spotOrder;
        float spotMinApart;
        int searchSteps;

        /// <summary>
        /// A spot for each hider from the kind's allowed list, each spot used once, spots at least 6 m apart if possible, none within
        /// 7 m of the child. When that is impossible the rules relax step by step; every hider ends up with a spot.
        /// </summary>
        void AssignSpots()
        {
            float[] apart = { SpotsApartMeters, 3f, 0f };
            foreach (bool respectPlayer in new[] { true, false })
            {
                foreach (float meters in apart)
                    if (TryAssign(meters, respectPlayer)) return;
            }

            // Last resort: any free spot at all.
            var free = new List<HidingSpot>(spots);
            Shuffle(free);
            foreach (Hider hider in hiders)
            {
                hider.spot = free.Count > 0 ? free[0] : null;
                if (free.Count > 0) free.RemoveAt(0);
            }
        }

        bool TryAssign(float minApart, bool respectPlayer)
        {
            int count = hiders.Count;
            spotOptions = new List<HidingSpot>[count];
            spotChoice = new HidingSpot[count];
            spotMinApart = minApart;
            searchSteps = 0;

            Vector3 playerPosition = PlayerPosition();
            for (int i = 0; i < count; i++)
            {
                var options = new List<HidingSpot>();
                foreach (HidingSpot spot in spots)
                {
                    if (spot == null || Array.IndexOf(hiders[i].kind.AllowedSpots, spot.SpotId) < 0) continue;
                    if (respectPlayer && FlatDistance(spot.CoverBounds.center, playerPosition) < MinSpotDistanceFromPlayer) continue;
                    options.Add(spot);
                }
                if (options.Count == 0) return false;

                Shuffle(options);
                spotOptions[i] = options;
            }

            // The hider with the fewest choices goes first.
            spotOrder = new int[count];
            for (int i = 0; i < count; i++) spotOrder[i] = i;
            Array.Sort(spotOrder, (a, b) => spotOptions[a].Count.CompareTo(spotOptions[b].Count));

            if (!Place(0)) return false;

            for (int i = 0; i < count; i++) hiders[i].spot = spotChoice[i];
            return true;
        }

        bool Place(int depth)
        {
            if (depth >= spotOrder.Length) return true;
            if (++searchSteps > MaxSearchSteps) return false;

            int index = spotOrder[depth];
            foreach (HidingSpot candidate in spotOptions[index])
            {
                bool fits = true;
                for (int d = 0; d < depth && fits; d++)
                {
                    HidingSpot other = spotChoice[spotOrder[d]];
                    if (other == candidate || FlatDistance(other.CoverBounds.center, candidate.CoverBounds.center) < spotMinApart) fits = false;
                }
                if (!fits) continue;

                spotChoice[index] = candidate;
                if (Place(depth + 1)) return true;
                spotChoice[index] = null;
            }
            return false;
        }

        // ----- Levels and progress -----------------------------------------------------------------

        int ByLevel(int[] table)
        {
            return table[Mathf.Clamp(level, 1, table.Length) - 1];
        }

        float ByLevel(float[] table)
        {
            return table[Mathf.Clamp(level, 1, table.Length) - 1];
        }

        int SeekCountTo => ByLevel(SeekCountByLevel);

        int HideCountTo => Mathf.Clamp(NumberCurriculum.MaxNumber(), 5, ByLevel(HideCountCapByLevel));

        /// <summary>
        /// Found kinds go into the saved progress (they stay in their pens). At the end of a finished round the rounds count goes up
        /// and the level moves: up after a round with no mistakes, down after three or more.
        /// </summary>
        void CommitProgress(bool roundFinished)
        {
            bool changed = false;
            foreach (Hider hider in foundOrder)
            {
                if (ZooProgress.IsKnown(hider.kind.Id)) continue;

                ZooProgress.MarkFound(hider.kind.Id);
                changed = true;
            }

            if (roundFinished && !roundCounted)
            {
                roundCounted = true;
                ProgressStore.Current.zooRounds++;
                changed = true;

                if (debugForceLevel <= 0)
                {
                    PlayerProgress progress = ProgressStore.Current;
                    int current = Mathf.Clamp(progress.GetLevel(Skill), 1, MaxLevel);
                    if (mistakes == 0 && current < MaxLevel) progress.SetLevel(Skill, current + 1);
                    else if (mistakes >= 3 && current > 1) progress.SetLevel(Skill, current - 1);
                }
            }

            if (changed) ProgressStore.Save();
        }

        // ----- Putting things back -----------------------------------------------------------------

        void RestorePlayer()
        {
            if (player == null) return;

            if (playerAnimator != null && playerDancing) playerAnimator.SetDancing(false);
            playerDancing = false;

            // Ended while he was hiding on a hide point: hop down in front of the spot (this also wakes the controller).
            if (player.IsRiding)
            {
                Vector3 ground = playerHiddenIn != null ? playerHiddenIn.PopPoint : player.transform.position;
                player.StopRiding(ground);
            }
            playerHiddenIn = null;

            // Ended in the middle of a hop with the controller off: switch it on again.
            var controller = player.GetComponent<CharacterController>();
            if (controller != null && !player.IsRiding) controller.enabled = true;
            player.InputEnabled = true;
        }

        /// <summary>
        /// Every animal that took part goes home: known kinds (the found ones were just added) stand in their pens, the others are
        /// hidden. The animals that did not take part were never touched.
        /// </summary>
        void PutAnimalsBack()
        {
            foreach (Hider hider in hiders)
            {
                if (hider.animal == null) continue;

                if (hider.pen != null && ZooProgress.IsKnown(hider.kind.Id)) hider.pen.PlaceInstantly(hider.animal);
                else hider.animal.Hide();
                if (hider.pen != null) hider.pen.RefreshSign();
            }
        }

        /// <summary>The spots, critters, glows and the trail back to the start. Safe to call at any time.</summary>
        void ResetWorld()
        {
            if (spots != null)
                foreach (HidingSpot spot in spots)
                    if (spot != null) spot.ResetSpot();
            if (critters != null)
                foreach (SurpriseCritter critter in critters)
                    if (critter != null) critter.Hide();
            if (trail != null) trail.Stop();
        }

        // ----- Helpers -----------------------------------------------------------------------------

        Vector3 PlayerPosition()
        {
            return player != null ? player.transform.position : transform.position;
        }

        void FreezePlayer(bool frozen)
        {
            if (player != null) player.InputEnabled = !frozen;
        }

        void SetHudVisible(bool visible)
        {
            if (lessonUI == null) return;

            lessonUI.SetGameplayHudVisible(visible);
            hudHidden = !visible;
        }

        static void Say(string group, bool interrupt = false)
        {
            VoicePlayer.Say(group, interrupt);
        }

        /// <summary>True when the voice table has a recording for the group (a line without a clip is only a debug subtitle).</summary>
        static bool HasClip(string group)
        {
            return VoicePlayer.TryGetLine(group, out AudioClip clip, out string text) && clip != null;
        }

        /// <summary>Waits for whoever is talking (and whoever is waiting to talk) to finish, but never for long.</summary>
        IEnumerator WaitForVoice(float maxSeconds = MaxVoiceWaitSeconds)
        {
            yield return new WaitForSeconds(0.3f);

            float giveUp = Time.time + maxSeconds;
            while (VoicePlayer.Instance != null && VoicePlayer.Instance.IsBusy && Time.time < giveUp)
                yield return null;
        }

        /// <summary>Runs an iterator, but gives up on it after that many seconds.</summary>
        static IEnumerator RunLimited(IEnumerator inner, float maxSeconds)
        {
            if (inner == null) yield break;

            float giveUp = Time.time + maxSeconds;
            while (Time.time < giveUp && inner.MoveNext())
                yield return inner.Current;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        ZooAnimal AnimalOf(ZooAnimalKind kind)
        {
            if (animals == null) return null;

            foreach (ZooAnimal animal in animals)
                if (animal != null && animal.KindId == kind.Id) return animal;

            int index = Array.IndexOf(ZooAnimalKind.Order, kind);
            return index >= 0 && index < animals.Length ? animals[index] : null;
        }

        ZooPen PenOf(ZooAnimalKind kind)
        {
            if (pens == null) return null;

            foreach (ZooPen pen in pens)
                if (pen != null && pen.KindId == kind.Id) return pen;

            int index = Array.IndexOf(ZooAnimalKind.Order, kind);
            return index >= 0 && index < pens.Length ? pens[index] : null;
        }

        Sprite IconOf(ZooAnimalKind kind)
        {
            int index = Array.IndexOf(ZooAnimalKind.Order, kind);
            return icons != null && index >= 0 && index < icons.Length ? icons[index] : null;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Walks the player to a point by hand (his controls are off), then turns him to face a direction.</summary>
        IEnumerator WalkPlayerTo(Vector3 point, Vector3 face, float maxSeconds)
        {
            if (player == null) yield break;

            var controller = player.GetComponent<CharacterController>();
            float giveUp = Time.time + maxSeconds;
            while (controller != null && controller.enabled && Time.time < giveUp)
            {
                Vector3 toPoint = point - player.transform.position;
                toPoint.y = 0f;
                float distance = toPoint.magnitude;
                if (distance < 0.25f) break;

                controller.Move(toPoint / distance * Mathf.Min(3.2f * Time.deltaTime, distance) + Vector3.down * 0.05f);
                player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(toPoint), 720f * Time.deltaTime);
                yield return null;
            }

            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                Quaternion wanted = Quaternion.LookRotation(face.normalized, Vector3.up);
                for (float elapsed = 0f; elapsed < 0.4f; elapsed += Time.deltaTime)
                {
                    player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, wanted, 720f * Time.deltaTime);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// The player hops to a point along an arc (the character controller sleeps for it). The caller finishes with
        /// player.Teleport, which wakes the controller again.
        /// </summary>
        IEnumerator PlayerHop(Vector3 end, float seconds)
        {
            if (player == null) yield break;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            if (playerAnimator != null) playerAnimator.PlayJump();

            Vector3 start = player.transform.position;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float k = elapsed / seconds;
                player.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.7f);
                yield return null;
            }
        }
    }
}
