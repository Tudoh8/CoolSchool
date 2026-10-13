using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Castle;
using CoolSchool.Lessons;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The fighting part of the castle adventure: two waves of goblins of different kinds, counting the goblins on the wall,
    /// the thief, the tiny goblin in the barrel and "help a friend".
    /// </summary>
    public partial class KnightsGame
    {
        const float SpawnGapSeconds = 0.6f;
        const float PerchSpawnGapSeconds = 0.3f;
        const float PerchSettleSeconds = 4f;
        const float JumpDownGapSeconds = 0.4f;
        const float LandWaitSeconds = 3f;
        const float WaveRestSeconds = 2.5f;
        const float WaitForFreeOpponentSeconds = 4f;
        // How long goblins that were sent home get to run to their ladder and vanish.
        const float SendHomeSeconds = 6f;
        // Safety net: if a wave somehow never finishes, send the rest home so the game goes on.
        const float WaveTimeoutSeconds = 120f;
        const float CountQuestionTimeoutSeconds = 100f;

        // Help a friend.
        const float HelpDelayMinSeconds = 6f;
        const float HelpDelayMaxSeconds = 10f;
        const float HelpFirstLandingWaitSeconds = 25f;
        const float HelpGiveUpSeconds = 25f;
        const float HelpNearMeters = 2.5f;

        /// <summary>What one wave is made of and what happens in it.</summary>
        sealed class WavePlan
        {
            public int Number;
            /// <summary>The goblins that climb the ladders (or stand on the perches), in the order they come.</summary>
            public readonly List<GoblinKind> Kinds = new List<GoblinKind>();
            /// <summary>The goblins stand on the wall perches first and are counted.</summary>
            public bool Perched;
            /// <summary>The tiny goblin hides in a barrel.</summary>
            public bool Tiny;
            /// <summary>The first normal goblin steals the mission's treasure.</summary>
            public bool Thief;
            /// <summary>A knight classmate calls for help once.</summary>
            public bool Help;
        }

        sealed class PerchedGoblin
        {
            public BattleOpponentAI Goblin;
            public int Index;
        }

        readonly List<PerchedGoblin> perched = new List<PerchedGoblin>();
        readonly HashSet<int> usedPerches = new HashSet<int>();
        BattleOpponentAI pickResult;
        List<BattleOpponentAI> currentWave = new List<BattleOpponentAI>();
        Coroutine helpRoutine;
        KnightAllyAI helpAlly;
        readonly List<BattleOpponentAI> helpChasers = new List<BattleOpponentAI>();

        // ---- The waves ----

        IEnumerator RunWaves()
        {
            yield return RunWave(PlanWaveOne());
            yield return RunWave(PlanWaveTwo());
        }

        /// <summary>
        /// Wave 1: with the goblin-counting moment they stand on the wall first (2-6 of them, kinds by level); otherwise two
        /// normal goblins and, from level 2, a shield goblin.
        /// </summary>
        WavePlan PlanWaveOne()
        {
            var plan = new WavePlan { Number = 1, Help = level >= 2 };

            if (numberMoment == CastleNumberMoment.Goblins && CanCountOnWall())
            {
                plan.Perched = true;
                int count = Mathf.Clamp(goblinCount, 2, Mathf.Min(wallPerches.Length, perchLandings.Length));
                if (level >= 3 && count >= 5)
                {
                    plan.Kinds.Add(GoblinKind.Big);
                    plan.Kinds.Add(GoblinKind.Shield);
                }
                else if (level >= 2)
                {
                    plan.Kinds.Add(GoblinKind.Shield);
                }
                while (plan.Kinds.Count < count) plan.Kinds.Add(GoblinKind.Normal);
            }
            else
            {
                plan.Kinds.Add(GoblinKind.Normal);
                plan.Kinds.Add(GoblinKind.Normal);
                if (level >= 2) plan.Kinds.Add(GoblinKind.Shield);
            }

            Shuffle(plan.Kinds);
            return plan;
        }

        /// <summary>Wave 2: normal, shield, a troll from level 2, the tiny one in the barrel, and the thief. Level 3 has no third wave.</summary>
        WavePlan PlanWaveTwo()
        {
            var plan = new WavePlan { Number = 2, Help = true, Tiny = true, Thief = CastleVisit.HasThief(mission) && LootFor(mission) != null };

            plan.Kinds.Add(GoblinKind.Normal);
            if (level >= 3) plan.Kinds.Add(GoblinKind.Normal);
            plan.Kinds.Add(GoblinKind.Shield);
            if (level >= 2) plan.Kinds.Add(GoblinKind.Big);

            Shuffle(plan.Kinds);
            return plan;
        }

        bool CanCountOnWall()
        {
            return lessonUI != null && wallPerches != null && perchLandings != null && wallPerches.Length >= 2 && perchLandings.Length >= 2;
        }

        IEnumerator RunWave(WavePlan plan)
        {
            // The wave line ("Here come the goblins!") comes from the onWaveStarted hook. It goes first: a kid's line is dropped
            // while the teacher talks, but the teacher's button explanation (wave 1) simply waits its turn.
            onWaveStarted?.Invoke(plan.Number);
            if (plan.Number == 1) VoicePlayer.Say("knights.start");
            Sfx.Play("battle.wave_horn");
            SetPlayerMode(PlayerMode.Fight);
            EnableAllies(true);

            var wave = new List<BattleOpponentAI>();
            currentWave = wave;
            Vector3 center = ArenaCenter();

            if (plan.Tiny) SpawnTiny(wave);
            if (plan.Perched)
            {
                yield return SpawnPerched(wave, plan);
                if (perched.Count > 0) yield return CountGoblinsOnWall();
                else plan.Perched = false;
            }
            if (!plan.Perched) yield return SpawnClimbers(wave, plan, center);

            if (plan.Help) helpRoutine = StartCoroutine(HelpFriendRoutine(wave));

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

            EndHelp();
            ResetBarrels();
            yield return new WaitForSeconds(WaveRestSeconds);
        }

        /// <summary>The goblins climb the ladders one after the other; the first normal one may become the thief.</summary>
        IEnumerator SpawnClimbers(List<BattleOpponentAI> wave, WavePlan plan, Vector3 center)
        {
            // Each wave starts on another ladder, so the goblins don't always climb the same ones.
            int firstLadder = ladders != null && ladders.Length > 0 ? Random.Range(0, ladders.Length) : 0;
            bool thiefPending = plan.Thief;
            MissionLoot loot = LootFor(mission);

            for (int i = 0; i < plan.Kinds.Count; i++)
            {
                yield return PickOpponentRoutine(wave, plan.Kinds[i]);
                BattleOpponentAI opponent = pickResult;
                if (opponent == null) continue;

                // The goblin starts behind the wall at the ladder's foot; the sparkle comes when it pops up over the wall.
                opponent.Activate(LadderFor(firstLadder + i), center);
                ApplyHits(opponent);
                wave.Add(opponent);

                if (thiefPending && loot != null && opponent.Kind == GoblinKind.Normal)
                {
                    thiefPending = false;
                    opponent.BecomeThief(loot, thiefRunPoints, "castle.thief_grab." + CastleVisit.Key(mission));
                }
                yield return new WaitForSeconds(SpawnGapSeconds);
            }
        }

        /// <summary>The tiny goblin goes into one of the barrels. It is not counted in the plan's kinds.</summary>
        void SpawnTiny(List<BattleOpponentAI> wave)
        {
            if (barrels == null || barrels.Length == 0) return;

            BattleOpponentAI tiny = FindOpponent(wave, GoblinKind.Tiny, true);
            if (tiny == null) return;

            CastleBarrel barrel = barrels[Random.Range(0, barrels.Length)];
            if (barrel == null) return;

            tiny.HideInBarrel(barrel, playArea);
            ApplyHits(tiny);
            wave.Add(tiny);
        }

        // ---- Counting the goblins on the wall ----

        /// <summary>The goblins hop up onto the wall perches, spread evenly, and wave.</summary>
        IEnumerator SpawnPerched(List<BattleOpponentAI> wave, WavePlan plan)
        {
            perched.Clear();
            usedPerches.Clear();
            int slots = Mathf.Min(wallPerches.Length, perchLandings.Length);
            int count = Mathf.Min(plan.Kinds.Count, slots);

            for (int i = 0; i < count; i++)
            {
                yield return PickOpponentRoutine(wave, plan.Kinds[i]);
                BattleOpponentAI opponent = pickResult;
                if (opponent == null) continue;

                int index = FreePerchNear(count == 1 ? slots / 2 : Mathf.RoundToInt(i * (slots - 1) / (float)(count - 1)), slots);
                if (index < 0) break;

                opponent.ActivateOnPerch(wallPerches[index], perchLandings[index].position);
                ApplyHits(opponent);
                wave.Add(opponent);
                perched.Add(new PerchedGoblin { Goblin = opponent, Index = index });
                usedPerches.Add(index);
                yield return new WaitForSeconds(PerchSpawnGapSeconds);
            }
        }

        /// <summary>The wanted perch, or the nearest free one. -1 when all are taken.</summary>
        int FreePerchNear(int wanted, int slots)
        {
            for (int distance = 0; distance < slots; distance++)
            {
                int left = wanted - distance;
                int right = wanted + distance;
                if (left >= 0 && left < slots && !usedPerches.Contains(left) && wallPerches[left] != null && perchLandings[left] != null) return left;
                if (right >= 0 && right < slots && !usedPerches.Contains(right) && wallPerches[right] != null && perchLandings[right] != null) return right;
            }
            return -1;
        }

        /// <summary>
        /// "Look, up on the wall! How many goblins do you see?" The child answers with number cards (counting along helps).
        /// From the one-more-one-less level on, one more pops up or one hops away and there is a second question. Then they all
        /// jump down and the fight starts.
        /// </summary>
        IEnumerator CountGoblinsOnWall()
        {
            // Wait until they are all up (or 4 s).
            float settleBy = Time.time + PerchSettleSeconds;
            while (Time.time < settleBy && !AllPerchedUp())
                yield return null;

            SetPlayerMode(PlayerMode.Frozen);
            EnableAllies(false);
            SetHud(false);

            yield return SayAndWait("castle.count");

            MomentResult result = NewResult();
            int cards = ByLevel(NumberCardsByLevel);
            bool ask = countingUnlocked && lessonUI != null;
            int onWall = PerchedCount();
            if (ask) yield return Bounded(CardQuestion.AskNumber(lessonUI, onWall, cards, CountAlongPerched, result), CountQuestionTimeoutSeconds);
            else yield return CountAlongPerched();

            // One more or one less, and a second question.
            if (ask && NumberCurriculum.IsUnlocked(NumberSkill.OneMoreOneLess) && Random.value < 0.5f)
            {
                bool canMore = CanOneMore(onWall);
                bool canLess = onWall >= 3;
                if (canMore || canLess)
                {
                    bool more = canMore && (!canLess || Random.value < 0.5f);
                    if (more) yield return OneMoreGoblin();
                    else yield return OneLessGoblin();

                    yield return SayAndWait("castle.count_now");
                    int now = PerchedCount();
                    yield return Bounded(CardQuestion.AskNumber(lessonUI, now, cards, CountAlongPerched, result), CountQuestionTimeoutSeconds);
                }
            }

            if (lessonUI != null) lessonUI.HideCards();
            SetHud(true);
            yield return SayAndWait("castle.jump_down");

            // They jump down one after another.
            foreach (PerchedGoblin entry in perched)
            {
                if (entry.Goblin != null && entry.Goblin.gameObject.activeInHierarchy) entry.Goblin.JumpDown();
                yield return new WaitForSeconds(JumpDownGapSeconds);
            }

            float landBy = Time.time + LandWaitSeconds;
            while (Time.time < landBy && !AllPerchedLanded())
                yield return null;

            perched.Clear();
            usedPerches.Clear();
            SetPlayerMode(PlayerMode.Fight);
            EnableAllies(true);
        }

        /// <summary>The goblins hop in turn, left to right, while their numbers are said.</summary>
        IEnumerator CountAlongPerched()
        {
            List<BattleOpponentAI> row = PerchedRow();
            for (int i = 0; i < row.Count; i++)
            {
                row[i].PerchHop();
                VoicePlayer.Say("number." + (i + 1), true);
                yield return new WaitForSeconds(0.9f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Another goblin pops onto a free perch ("Me too!").</summary>
        IEnumerator OneMoreGoblin()
        {
            int slots = Mathf.Min(wallPerches.Length, perchLandings.Length);
            var free = new List<int>();
            for (int i = 0; i < slots; i++)
                if (!usedPerches.Contains(i) && wallPerches[i] != null && perchLandings[i] != null) free.Add(i);
            if (free.Count == 0) yield break;

            BattleOpponentAI extra = FindOpponent(currentWave, GoblinKind.Normal, true);
            if (extra == null) yield break;

            int index = free[Random.Range(0, free.Count)];
            extra.ActivateOnPerch(wallPerches[index], perchLandings[index].position);
            ApplyHits(extra);
            currentWave.Add(extra);
            perched.Add(new PerchedGoblin { Goblin = extra, Index = index });
            usedPerches.Add(index);

            VoicePlayer.Say("castle.count_more", true);
            float upBy = Time.time + 2f;
            while (Time.time < upBy && !extra.IsOnPerch)
                yield return null;
            yield return WaitForVoice();
        }

        /// <summary>The last goblin in the row hops back down behind the wall ("Eek, I'm scared!").</summary>
        IEnumerator OneLessGoblin()
        {
            List<BattleOpponentAI> row = PerchedRow();
            if (row.Count == 0) yield break;

            BattleOpponentAI leaver = row[row.Count - 1];
            VoicePlayer.Say("castle.count_less", true);
            leaver.HopBackDown();
            yield return new WaitForSeconds(1f);
            yield return WaitForVoice();

            for (int i = perched.Count - 1; i >= 0; i--)
                if (perched[i].Goblin == leaver)
                {
                    usedPerches.Remove(perched[i].Index);
                    perched.RemoveAt(i);
                }
        }

        bool CanOneMore(int onWall)
        {
            int slots = Mathf.Min(wallPerches.Length, perchLandings.Length);
            if (onWall + 1 > Mathf.Min(slots, Mathf.Max(2, NumberCurriculum.MaxNumber()))) return false;

            return FindOpponent(currentWave, GoblinKind.Normal, true) != null;
        }

        /// <summary>The goblins standing on the wall, left to right as the child sees them.</summary>
        List<BattleOpponentAI> PerchedRow()
        {
            var row = new List<PerchedGoblin>();
            foreach (PerchedGoblin entry in perched)
                if (entry.Goblin != null && entry.Goblin.gameObject.activeInHierarchy) row.Add(entry);
            row.Sort((a, b) => wallPerches[a.Index].position.x.CompareTo(wallPerches[b.Index].position.x));

            var goblins = new List<BattleOpponentAI>(row.Count);
            foreach (PerchedGoblin entry in row) goblins.Add(entry.Goblin);
            return goblins;
        }

        int PerchedCount()
        {
            return PerchedRow().Count;
        }

        bool AllPerchedUp()
        {
            foreach (PerchedGoblin entry in perched)
                if (entry.Goblin != null && entry.Goblin.gameObject.activeInHierarchy && !entry.Goblin.IsOnPerch) return false;
            return true;
        }

        bool AllPerchedLanded()
        {
            foreach (PerchedGoblin entry in perched)
                if (entry.Goblin != null && entry.Goblin.gameObject.activeInHierarchy && !entry.Goblin.HasLanded) return false;
            return true;
        }

        // ---- Help a friend ----

        /// <summary>
        /// 6 to 10 s after the first goblin has landed, a knight classmate is chased by the one or two goblins nearest to him
        /// and calls for help ("!" above his head). It ends when the child is next to him, the goblins are out, or after 25 s.
        /// </summary>
        IEnumerator HelpFriendRoutine(List<BattleOpponentAI> wave)
        {
            float giveUp = Time.time + HelpFirstLandingWaitSeconds;
            while (Time.time < giveUp && !AnyLanded(wave))
                yield return null;
            yield return new WaitForSeconds(Random.Range(HelpDelayMinSeconds, HelpDelayMaxSeconds));

            KnightAllyAI ally = PickHelpAlly();
            if (ally == null)
            {
                helpRoutine = null;
                yield break;
            }

            var chasers = NearestGoblins(wave, ally, 2);
            if (chasers.Count == 0)
            {
                helpRoutine = null;
                yield break;
            }

            helpAlly = ally;
            helpChasers.Clear();
            Hittable allyHittable = ally.GetComponent<Hittable>();
            foreach (BattleOpponentAI goblin in chasers)
            {
                goblin.ForceTarget(allyHittable);
                helpChasers.Add(goblin);
            }
            ally.CallForHelp(true);
            VoicePlayer.Say("castle.help_me");

            bool timedOut = true;
            float end = Time.time + HelpGiveUpSeconds;
            while (Time.time < end)
            {
                if (FlatDistance(PlayerPosition(), ally.transform.position) <= HelpNearMeters || AllOut(helpChasers))
                {
                    timedOut = false;
                    break;
                }
                yield return null;
            }

            helpRoutine = null;
            ReleaseHelp();
            if (!timedOut) VoicePlayer.Say("castle.thanks");
        }

        /// <summary>Stops a help call: the "!" goes away and the goblins pick their own targets again.</summary>
        void EndHelp()
        {
            if (helpRoutine != null) StopCoroutine(helpRoutine);
            helpRoutine = null;
            ReleaseHelp();
        }

        void ReleaseHelp()
        {
            if (helpAlly != null) helpAlly.CallForHelp(false);
            helpAlly = null;
            foreach (BattleOpponentAI goblin in helpChasers)
                if (goblin != null) goblin.ClearForcedTarget();
            helpChasers.Clear();
        }

        KnightAllyAI PickHelpAlly()
        {
            if (allies == null) return null;

            var candidates = new List<KnightAllyAI>();
            foreach (KnightAllyAI ally in allies)
                if (ally != null && ally != stuckKnight && ally.enabled && ally.gameObject.activeInHierarchy) candidates.Add(ally);
            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        /// <summary>The nearest goblins (not the thief, not the tiny one) that are fighting in the courtyard.</summary>
        static List<BattleOpponentAI> NearestGoblins(List<BattleOpponentAI> wave, KnightAllyAI ally, int count)
        {
            var found = new List<BattleOpponentAI>();
            foreach (BattleOpponentAI goblin in wave)
            {
                if (goblin == null || goblin.IsOut || !goblin.HasLanded || goblin.IsThief || goblin.Kind == GoblinKind.Tiny) continue;

                found.Add(goblin);
            }
            found.Sort((a, b) => FlatDistance(a.transform.position, ally.transform.position)
                .CompareTo(FlatDistance(b.transform.position, ally.transform.position)));
            if (found.Count > count) found.RemoveRange(count, found.Count - count);
            return found;
        }

        static bool AnyLanded(List<BattleOpponentAI> wave)
        {
            foreach (BattleOpponentAI goblin in wave)
                if (goblin != null && goblin.gameObject.activeInHierarchy && goblin.HasLanded && goblin.Kind != GoblinKind.Tiny) return true;
            return false;
        }

        void ClearHelpMarkers()
        {
            EndHelp();
            if (allies == null) return;

            foreach (KnightAllyAI ally in allies)
                if (ally != null) ally.CallForHelp(false);
        }

        // ---- Pool and helpers ----

        /// <summary>Hits to bump down, by kind and level (the golden sword still doubles every hit; the thief always needs one).</summary>
        void ApplyHits(BattleOpponentAI opponent)
        {
            var hittable = opponent.GetComponent<Hittable>();
            if (hittable == null) return;

            int hits;
            switch (opponent.Kind)
            {
                case GoblinKind.Shield: hits = ByLevel(HitsShieldByLevel); break;
                case GoblinKind.Big: hits = ByLevel(HitsBigByLevel); break;
                case GoblinKind.Tiny: hits = ByLevel(HitsTinyByLevel); break;
                default: hits = ByLevel(HitsNormalByLevel); break;
            }
            hittable.SetHitsToDefeat(hits);
        }

        /// <summary>
        /// Looks for a free goblin of this kind (waiting a few seconds for one that is still running home). When the kind is not in
        /// the pool, another normal goblin steps in. Leaves the result in pickResult (null when nobody is free).
        /// </summary>
        IEnumerator PickOpponentRoutine(List<BattleOpponentAI> wave, GoblinKind kind)
        {
            float giveUpTime = Time.time + WaitForFreeOpponentSeconds;
            pickResult = FindOpponent(wave, kind, true);
            while (pickResult == null && Time.time < giveUpTime)
            {
                yield return null;
                pickResult = FindOpponent(wave, kind, true);
            }

            // Nobody free: reuse one that is still running home, or a normal goblin instead of the missing kind.
            if (pickResult == null) pickResult = FindOpponent(wave, kind, false);
            if (pickResult == null && kind != GoblinKind.Tiny) pickResult = FindOpponent(wave, GoblinKind.Normal, true);
        }

        /// <summary>Prefers opponents that are switched off, so nobody pops in mid-run.</summary>
        BattleOpponentAI FindOpponent(List<BattleOpponentAI> alreadyInWave, GoblinKind kind, bool onlyInactive)
        {
            if (opponents == null) return null;

            foreach (BattleOpponentAI opponent in opponents)
            {
                if (opponent == null || opponent.Kind != kind || alreadyInWave.Contains(opponent)) continue;
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

        void ResetBarrels()
        {
            if (barrels == null) return;

            foreach (CastleBarrel barrel in barrels)
                if (barrel != null) barrel.ResetBarrel();
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
