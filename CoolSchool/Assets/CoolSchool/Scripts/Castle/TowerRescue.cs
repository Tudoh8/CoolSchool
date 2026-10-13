using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The knight mission: one of the classmate knights is stuck on top of the back tower and waves for help. After the goblins,
    /// the knight walks to the glowing ring at the foot of the tower, a ladder sparkles into place and the stuck knight hops down
    /// it in three hops and joins the others. While he is up there he is out of the fight: no brain, no navigation, not a target.
    /// </summary>
    public class TowerRescue : MonoBehaviour
    {
        [Tooltip("The middle of the tower's roof, where the stuck knight stands.")]
        [SerializeField] Transform towerTop;
        [Tooltip("A glow ring on the courtyard floor at the foot of the tower. Hidden at the start.")]
        [SerializeField] GameObject rescueRing;
        [Tooltip("The ladder leaning from the roof to the ring. Hidden at the start.")]
        [SerializeField] GameObject ladder;

        const float NearRingMeters = 1.6f;
        const float HintSeconds = 15f;
        const float GiveUpSeconds = 40f;
        const float CheerEverySeconds = 3f;

        KnightAllyAI ally;
        NavMeshAgent agent;
        Hittable hittable;
        KidAnimator kid;
        Transform originalParent;
        Vector3 originalScale;
        bool placed;
        Coroutine cheering;

        /// <summary>The knight who is stuck up there, or null.</summary>
        public KnightAllyAI StuckKnight => placed ? ally : null;

        /// <summary>The knight is put on the tower: out of the fight, waving, with the "!" over his head.</summary>
        public void PlaceKnight(KnightAllyAI knight)
        {
            if (knight == null || towerTop == null) return;

            if (placed) ResetMoment();

            ally = knight;
            agent = knight.GetComponent<NavMeshAgent>();
            hittable = knight.GetComponent<Hittable>();
            kid = knight.GetComponent<KidAnimator>();
            originalParent = knight.transform.parent;
            originalScale = knight.transform.lossyScale;
            placed = true;

            // Out of the fight: no brain, no navigation, and goblins can not pick him as a target.
            knight.CallForHelp(false);
            knight.enabled = false;
            if (hittable != null) hittable.enabled = false;
            if (agent != null) agent.enabled = false;

            knight.transform.SetParent(towerTop, true);
            knight.transform.position = towerTop.position;
            knight.transform.rotation = Quaternion.LookRotation(FlatToward(towerTop.position, Camera.main), Vector3.up);
            KeepWorldScale(knight.transform);

            knight.CallForHelp(true);
            if (isActiveAndEnabled) cheering = StartCoroutine(CheerLoop());
        }

        /// <summary>
        /// The knight walks to the ring (or is called there after 40 s), the ladder appears and the stuck knight comes down in three
        /// hops. His brain stays off: the dragon arrives next and nobody fights any more.
        /// </summary>
        public IEnumerator Run(Transform player)
        {
            if (!placed || ally == null) yield break;

            if (rescueRing != null) rescueRing.SetActive(true);
            Vector3 ring = rescueRing != null ? rescueRing.transform.position : towerTop.position;

            float started = Time.time;
            float nextHint = Time.time + HintSeconds;
            while (player != null && Time.time - started < GiveUpSeconds
                && CastleMotion.FlatDistance(player.position, ring) > NearRingMeters)
            {
                if (Time.time >= nextHint && CastleMotion.VoiceIsFree())
                {
                    nextHint = Time.time + HintSeconds;
                    VoicePlayer.Say("castle.rescue");
                }
                yield return null;
            }

            // The ladder sparkles into place.
            if (ladder != null)
            {
                Vector3 ladderScale = ladder.transform.localScale;
                ladder.SetActive(true);
                Sfx.Play("castle.ladder_appear", ladder.transform.position);
                BattleEffects.Sparkle(ladder.transform.position + Vector3.up * 1.5f);
                yield return CastleMotion.PopIn(ladder.transform, ladderScale, 0.5f);
            }

            // Three hops along the ladder, from the roof to the ring.
            Vector3 top = ally.transform.position;
            Vector3 bottom = ring;
            if (NavMesh.SamplePosition(bottom, out NavMeshHit hit, 2f, NavMesh.AllAreas)) bottom = hit.position;

            ally.transform.SetParent(originalParent, true);
            KeepWorldScale(ally.transform);
            if (cheering != null) StopCoroutine(cheering);
            cheering = null;
            ally.CallForHelp(false);

            for (int hop = 1; hop <= 3; hop++)
            {
                Vector3 target = Vector3.Lerp(top, bottom, hop / 3f);
                if (kid != null) kid.PlayJump();
                Vector3 look = bottom - ally.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) ally.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                yield return CastleMotion.Arc(ally.transform, target, 0.5f, 0.5f);
                Sfx.Play("player.land", target);
            }

            // Down: back on the ground and on the navigation mesh.
            Landed(bottom);
            if (kid != null) kid.PlayCheer();
            BattleEffects.Confetti(bottom + Vector3.up * 1.2f);
            yield return CastleMotion.SayAndWait("castle.rescue_down");

            if (ladder != null)
            {
                BattleEffects.Sparkle(ladder.transform.position + Vector3.up * 1.5f);
                ladder.SetActive(false);
            }
            if (rescueRing != null) rescueRing.SetActive(false);

            // He is free: from now on he is a normal knight again and ResetMoment leaves him where he is.
            placed = false;
        }

        /// <summary>The knight back to normal (on the ground by the ring, with his navigation, not a target), ring and ladder hidden.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            cheering = null;

            if (rescueRing != null) rescueRing.SetActive(false);
            if (ladder != null) ladder.SetActive(false);

            if (placed && ally != null)
            {
                ally.CallForHelp(false);
                ally.transform.SetParent(originalParent, true);
                KeepWorldScale(ally.transform);
                Vector3 ring = rescueRing != null ? rescueRing.transform.position : towerTop != null ? towerTop.position : ally.transform.position;
                if (NavMesh.SamplePosition(ring, out NavMeshHit hit, 3f, NavMesh.AllAreas)) ring = hit.position;
                Landed(ring);
            }

            placed = false;
            ally = null;
            agent = null;
            hittable = null;
            kid = null;
        }

        // ----- Helpers ---------------------------------------------------------------------------

        /// <summary>On the ground at a point: the navigation and the hit box are on again, his brain stays off.</summary>
        void Landed(Vector3 point)
        {
            ally.transform.position = point;
            if (hittable != null)
            {
                hittable.ResetState();
                hittable.enabled = true;
            }
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(point);
                else if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
                if (agent.isOnNavMesh) agent.isStopped = true;
            }
        }

        IEnumerator CheerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(CheerEverySeconds);
                if (kid != null) kid.PlayCheer();
            }
        }

        /// <summary>The knight keeps his size when the tower (which may be scaled) becomes or stops being his parent.</summary>
        void KeepWorldScale(Transform knight)
        {
            Vector3 parentScale = knight.parent != null ? knight.parent.lossyScale : Vector3.one;
            knight.localScale = new Vector3(Safe(originalScale.x, parentScale.x), Safe(originalScale.y, parentScale.y), Safe(originalScale.z, parentScale.z));
        }

        static float Safe(float value, float by)
        {
            return Mathf.Abs(by) < 0.0001f ? value : value / by;
        }

        /// <summary>The way to look from a point toward the camera (south), flattened.</summary>
        static Vector3 FlatToward(Vector3 from, Camera camera)
        {
            Vector3 direction = camera != null ? camera.transform.position - from : Vector3.back;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.01f ? Vector3.back : direction.normalized;
        }
    }
}
