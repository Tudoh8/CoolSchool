using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Castle;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The first part of the castle adventure: choosing this visit's mission and moments, the queen at the gate, the password,
    /// the walk into the courtyard and the hole in the wall. Also the level table and the adaptive level.
    /// </summary>
    public partial class KnightsGame
    {
        // The adaptive level: 1 to 3, read when the visit starts (a visit with no mistakes goes up, two or more go down).
        const string Skill = "castle";
        const int MaxLevel = 3;

        // The level table of the contract (index = level - 1). Numbers are always capped by NumberCurriculum.MaxNumber().
        static readonly int[] WallBlocksMin = { 3, 4, 5 };
        static readonly int[] WallBlocksMax = { 3, 5, 6 };
        static readonly int[] GoblinCountMin = { 2, 3, 4 };
        static readonly int[] GoblinCountMax = { 3, 5, 6 };
        static readonly int[] CookiesMin = { 2, 3, 4 };
        static readonly int[] CookiesMax = { 3, 5, 6 };
        static readonly int[] NumberCardsByLevel = { 2, 3, 3 };
        static readonly int[] HitsNormalByLevel = { 1, 2, 2 };
        static readonly int[] HitsShieldByLevel = { 1, 2, 2 };
        static readonly int[] HitsBigByLevel = { 3, 3, 3 };
        static readonly int[] HitsTinyByLevel = { 1, 1, 1 };

        // Waiting for the child: when he is walked to the wall by himself, and the safety nets around the moments.
        const float WalkHimToWallSeconds = 25f;
        const float WallTrailHintSeconds = 10f;
        const float WallArriveMeters = 1.6f;
        const float PasswordTimeoutSeconds = 100f;
        const float WallTimeoutSeconds = 100f;
        const float GateOpenWaitSeconds = 3f;
        // The sparkle trail that shows the way (the same as the pirates' and the inviter's).
        const float TrailSpacing = 1.5f;
        const float TrailHeight = 0.3f;
        const int MaxTrailPoints = 16;

        CastleMission mission;
        CastleNumberMoment numberMoment;
        CastleLetterMoment letterMoment;
        int level = 1;
        int wallBlocks;
        int goblinCount;
        int cookieCount;
        bool countingUnlocked;
        KnightAllyAI stuckKnight;
        Bounds playArea;
        readonly List<MomentResult> results = new List<MomentResult>();

        // ---- Choosing the visit ----

        /// <summary>
        /// Picks this visit's mission (the first visit ever is the cake, then a random one that is not the same as last time),
        /// the number moment (not the same as last time, the egg always counts goblins) and the letter moment, and saves them.
        /// </summary>
        void PickVisit()
        {
            PlayerProgress progress = ProgressStore.Current;
            level = Mathf.Clamp(progress.GetLevel(Skill), 1, MaxLevel);

            CastleMission lastMission = CastleMission.Cake;
            bool hasLastMission = !string.IsNullOrEmpty(progress.castleLastMission)
                && Enum.TryParse(progress.castleLastMission, true, out lastMission);
            mission = progress.castleVisits <= 0 ? CastleMission.Cake : PickOther(Enum.GetValues(typeof(CastleMission)), lastMission, hasLastMission);

            if (mission == CastleMission.Egg)
            {
                numberMoment = CastleNumberMoment.Goblins;
            }
            else
            {
                bool hasLastMoment = Enum.TryParse(progress.castleLastNumberMoment, true, out CastleNumberMoment lastMoment);
                numberMoment = hasLastMoment
                    ? (lastMoment == CastleNumberMoment.Goblins ? CastleNumberMoment.Cookies : CastleNumberMoment.Goblins)
                    : (Random.value < 0.5f ? CastleNumberMoment.Goblins : CastleNumberMoment.Cookies);
            }

            if (mission == CastleMission.Egg && string.IsNullOrEmpty(progress.babyDragonLetter) && naming != null)
                letterMoment = CastleLetterMoment.NameBabyDragon;
            else if (nameFlag != null && nameFlag.CanRun)
                letterMoment = CastleLetterMoment.NameFlag;
            else
                letterMoment = CastleLetterMoment.None;

            countingUnlocked = NumberCurriculum.IsUnlocked(NumberSkill.Counting);
            wallBlocks = PickNumber(WallBlocksMin, WallBlocksMax);
            goblinCount = PickNumber(GoblinCountMin, GoblinCountMax);
            cookieCount = Mathf.Min(8, PickNumber(CookiesMin, CookiesMax));

            progress.castleVisits++;
            progress.castleLastMission = mission.ToString();
            progress.castleLastNumberMoment = numberMoment.ToString();
            ProgressStore.Save();
        }

        static CastleMission PickOther(Array all, CastleMission avoid, bool hasAvoid)
        {
            var options = new List<CastleMission>();
            foreach (object value in all)
            {
                var candidate = (CastleMission)value;
                if (!hasAvoid || candidate != avoid) options.Add(candidate);
            }
            return options[Random.Range(0, options.Count)];
        }

        /// <summary>A number between the level's two table values, never over the biggest number he knows (and at least 2).</summary>
        int PickNumber(int[] min, int[] max)
        {
            int cap = Mathf.Max(2, NumberCurriculum.MaxNumber());
            int index = Mathf.Clamp(level, 1, MaxLevel) - 1;
            int low = Mathf.Min(min[index], cap);
            int high = Mathf.Max(low, Mathf.Min(max[index], cap));
            return Random.Range(low, high + 1);
        }


        int ByLevel(int[] table)
        {
            return table[Mathf.Clamp(level, 1, table.Length) - 1];
        }

        /// <summary>Hides all mission props and shows this visit's; sends the last knight up the tower in the knight mission.</summary>
        void SetUpMission()
        {
            if (missionProps != null)
            {
                missionProps.ResetAll();
                missionProps.Show(mission);
            }
            ResetLoot();

            stuckKnight = null;
            if (mission == CastleMission.Knight && towerRescue != null && allies != null)
            {
                for (int i = allies.Length - 1; i >= 0; i--)
                {
                    if (allies[i] == null) continue;

                    stuckKnight = allies[i];
                    towerRescue.PlaceKnight(stuckKnight);
                    break;
                }
            }

            playArea = ComputePlayArea();
        }

        /// <summary>
        /// The courtyard floor the small goblin may run around on: the box around the arena centre, the thief's run points,
        /// the perch landings and the ladder landings.
        /// </summary>
        Bounds ComputePlayArea()
        {
            var area = new Bounds(ArenaCenter(), Vector3.zero);
            if (celebrateSpot != null) area.Encapsulate(celebrateSpot.position);
            if (thiefRunPoints != null)
                foreach (Transform point in thiefRunPoints)
                    if (point != null) area.Encapsulate(point.position);
            if (perchLandings != null)
                foreach (Transform point in perchLandings)
                    if (point != null) area.Encapsulate(point.position);
            if (ladders != null)
                foreach (WallLadder ladder in ladders)
                    if (ladder != null) area.Encapsulate(ladder.Landing);
            if (barrels != null)
                foreach (CastleBarrel barrel in barrels)
                    if (barrel != null) area.Encapsulate(barrel.transform.position);
            area.Expand(new Vector3(0.5f, 4f, 0.5f));
            return area;
        }

        MomentResult NewResult()
        {
            var result = new MomentResult();
            results.Add(result);
            return result;
        }

        MissionLoot LootFor(CastleMission forMission)
        {
            switch (forMission)
            {
                case CastleMission.Cake: return cakeLoot;
                case CastleMission.Crown: return crownLoot;
                case CastleMission.Egg: return eggLoot;
                default: return null;
            }
        }

        void ResetLoot()
        {
            if (cakeLoot != null) cakeLoot.ResetLoot();
            if (crownLoot != null) crownLoot.ResetLoot();
            if (eggLoot != null) eggLoot.ResetLoot();
        }

        /// <summary>Puts every castle moment back to the start. Safe to call at any time, also twice.</summary>
        void ResetCastleMoments()
        {
            if (password != null) password.ResetMoment();
            if (wall != null) wall.ResetMoment();
            if (cookies != null) cookies.ResetMoment();
            if (egg != null) egg.ResetMoment();
            if (naming != null) naming.ResetMoment();
            if (nameFlag != null) nameFlag.ResetMoment();
            if (towerRescue != null) towerRescue.ResetMoment();
            if (guide != null) guide.ResetMoment();
            if (growth != null) growth.ResetMoment();
            if (ridePath != null) ridePath.ResetStars();
            if (babyDragon != null) babyDragon.Hide();
            if (missionProps != null) missionProps.ResetAll();
            ResetLoot();
            if (queen != null) queen.ResetQueen();
        }

        // ---- At the gate ----

        /// <summary>
        /// The child walks to the talk spot in front of the gate; the queen waves and tells the mission; the password opens
        /// the gate.
        /// </summary>
        IEnumerator RunGateScene()
        {
            SetPlayerMode(PlayerMode.Frozen);

            Vector3 north = Vector3.forward;
            if (gateTalkSpot != null) yield return MovePlayerTo(gateTalkSpot.position, north);
            else if (player != null) yield return TurnToFace(player.transform, north, 720f);

            if (queen != null) queen.Wave();
            yield return SayAndWait("castle.queen_hello");
            yield return SayAndWait("castle.mission." + CastleVisit.Key(mission));
            if (mission == CastleMission.Knight) yield return SayAndWait("castle.tower_help");

            // The password says "castle.password_right" itself.
            if (password != null)
                yield return Bounded(password.Run(level, letters, NewResult()), PasswordTimeoutSeconds);

            // The gate rises because the activity runs; give it a moment before he walks in.
            if (portcullis != null)
            {
                portcullis.HoldClosed = false;
                float openBy = Time.time + GateOpenWaitSeconds;
                while (portcullis.Openness < 0.9f && Time.time < openBy)
                    yield return null;
            }
        }

        // ---- In the courtyard ----

        /// <summary>
        /// He walks in to the hole in the wall (the guide ring and arrow lead him; after 25 s he is walked there), and fills the
        /// hole with blocks (counting along). "castle.wall" is said by the wall patch itself.
        /// </summary>
        IEnumerator RunCourtyard()
        {
            SetPlayerMode(PlayerMode.Walk);

            if (queen != null)
                queen.WalkTo(queenSpot != null ? queenSpot.position : ArenaCenter() + new Vector3(4f, 0f, -3f));
            if (wall == null) yield break;

            Vector3 standPoint = wall.StandPoint;
            if (guide != null) guide.ShowAt(standPoint);

            float start = Time.time;
            float nextTrail = start + WallTrailHintSeconds;
            while (FlatDistance(PlayerPosition(), standPoint) > WallArriveMeters)
            {
                if (Time.time - start >= WalkHimToWallSeconds)
                {
                    SetPlayerMode(PlayerMode.Frozen);
                    yield return MovePlayerTo(standPoint, Vector3.forward);
                    break;
                }

                if (Time.time >= nextTrail)
                {
                    nextTrail = Time.time + 5f;
                    ShowTrailTo(standPoint, WallArriveMeters);
                }
                yield return null;
            }

            if (guide != null) guide.Hide();
            SetPlayerMode(PlayerMode.Frozen);
            if (player != null) yield return TurnToFace(player.transform, Vector3.forward, 720f);

            yield return Bounded(wall.Run(wallBlocks, NewResult()), WallTimeoutSeconds);
            SetPlayerMode(PlayerMode.Walk);
        }

        /// <summary>Sparkles every few metres along the straight line from the player toward the target.</summary>
        void ShowTrailTo(Vector3 target, float stopRadius)
        {
            if (player == null) return;

            Vector3 start = player.transform.position;
            Vector3 toTarget = target - start;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < stopRadius + 1f) return;

            Vector3 direction = toTarget / distance;
            float end = Mathf.Max(0f, distance - stopRadius);
            float along = 1f;
            for (int i = 0; i < MaxTrailPoints && along <= end; i++)
            {
                BattleEffects.Sparkle(start + direction * along + Vector3.up * TrailHeight);
                along += TrailSpacing;
            }
        }

        // ---- The adaptive level ----

        /// <summary>
        /// No mistakes in the whole visit: one level up (to 3); two or more: one level down (to 1). Naming the baby dragon
        /// never counts (it has no result). Only called when the visit was finished.
        /// </summary>
        void AdaptLevel()
        {
            int mistakes = 0;
            foreach (MomentResult result in results)
                if (result != null) mistakes += result.Mistakes;

            PlayerProgress progress = ProgressStore.Current;
            int current = Mathf.Clamp(progress.GetLevel(Skill), 1, MaxLevel);
            if (mistakes == 0 && current < MaxLevel) progress.SetLevel(Skill, current + 1);
            else if (mistakes >= 2 && current > 1) progress.SetLevel(Skill, current - 1);
            ProgressStore.Save();
        }
    }
}
