using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// "There is a hole in the wall!" A goblin peeks through the hole and the knight patches it with toy blocks: he taps the blocks
    /// in the heap and each one flies into the next empty place in the hole while its number is said. Counting along, no cards, no
    /// mistakes. A quiet child is reminded after 8 s and helped (the blocks fly in by themselves) 30 s later.
    /// </summary>
    public class WallPatch : MonoBehaviour
    {
        [Tooltip("Where the knight stands to patch the wall (3.6 m in front of the hole).")]
        [SerializeField] Transform standPoint;
        [Tooltip("8 places in the hole, bottom left first, row by row.")]
        [SerializeField] Transform[] slots;
        [Tooltip("A see-through ghost block in each place (8).")]
        [SerializeField] GameObject[] ghosts;
        [Tooltip("10 toy blocks lying in a heap.")]
        [SerializeField] Transform[] pileBlocks;
        [Tooltip("The goblin behind the hole. It moves up 0.5 m to peek.")]
        [SerializeField] Transform peekGoblin;
        [SerializeField] WorldTapPicker picker;

        const float HintSeconds = 8f;
        const float AutoSeconds = 30f;
        const float FlySeconds = 0.5f;
        const float PeekHeight = 0.5f;
        const float AutoGapSeconds = 1.1f;

        /// <summary>Where the knight stands to patch the hole.</summary>
        public Vector3 StandPoint => standPoint != null ? standPoint.position : transform.position;

        sealed class Home
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        readonly Dictionary<Transform, Home> homes = new Dictionary<Transform, Home>();
        readonly List<Transform> pending = new List<Transform>();
        readonly List<Transform> placedBlocks = new List<Transform>();
        Vector3 peekRest;
        bool haveRest;
        int placed;
        int flying;
        Coroutine peekFork;

        /// <summary>The whole moment: the hole, the peeking goblin, the blocks. Returns when the patch is full and the goblin has gone.</summary>
        public IEnumerator Run(int blockCount, MomentResult result)
        {
            CacheHomes();
            int total = Mathf.Clamp(blockCount, 1, Mathf.Min(slots != null ? slots.Length : 0, pileBlocks != null ? pileBlocks.Length : 0));
            if (total <= 0) yield break;

            placed = 0;
            flying = 0;
            pending.Clear();

            if (ghosts != null)
                for (int i = 0; i < ghosts.Length; i++)
                    if (ghosts[i] != null) ghosts[i].SetActive(i < total);

            var targets = new List<Transform>();
            foreach (Transform block in pileBlocks)
                if (block != null) targets.Add(block);
            if (picker != null)
            {
                picker.SetTargets(targets);
                picker.Tapped += OnTapped;
                picker.Armed = true;
            }

            VoicePlayer.Say("castle.wall");
            peekFork = this.Fork(PeekRoutine("castle.wall_peek", 3f, 1.8f, false));

            float lastActivity = Time.time;
            bool hinted = false;
            float nextAuto = 0f;
            while (placed < total || flying > 0)
            {
                // Taps that came in: each takes the next empty place.
                while (pending.Count > 0 && placed < total)
                {
                    Transform block = pending[0];
                    pending.RemoveAt(0);
                    if (block == null || placedBlocks.Contains(block)) continue;

                    lastActivity = Time.time;
                    hinted = false;
                    SendToSlot(block, targets);
                }
                pending.Clear();

                if (placed < total)
                {
                    float quiet = Time.time - lastActivity;
                    if (!hinted && quiet >= HintSeconds)
                    {
                        hinted = true;
                        VoicePlayer.Say("castle.wall_hint");
                    }
                    else if (quiet >= HintSeconds + AutoSeconds && Time.time >= nextAuto)
                    {
                        // Solved for him: the blocks fly in one by one, counted out loud.
                        nextAuto = Time.time + AutoGapSeconds;
                        Transform next = NextFreeBlock(targets);
                        if (next != null) SendToSlot(next, targets);
                    }
                }
                yield return null;
            }

            if (picker != null)
            {
                picker.Armed = false;
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            // The patch is full: the goblin pops up once more, says it is closed, and drops away. Sparkles over the patch.
            yield return new WaitForSeconds(0.3f);
            if (slots != null && slots[0] != null)
            {
                Vector3 middle = (slots[0].position + slots[Mathf.Min(total, slots.Length) - 1].position) * 0.5f;
                BattleEffects.Sparkle(middle);
                BattleEffects.Confetti(middle + Vector3.up * 0.5f);
            }
            if (peekFork != null) StopCoroutine(peekFork);
            peekFork = null;
            yield return PeekRoutine("castle.wall_done", 0f, 1.5f, true);
        }

        /// <summary>Blocks back on the heap, ghosts and goblin hidden, picker released. Safe at any moment.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            if (picker != null)
            {
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            pending.Clear();
            placedBlocks.Clear();
            placed = 0;
            flying = 0;

            foreach (KeyValuePair<Transform, Home> entry in homes)
            {
                if (entry.Key == null) continue;

                entry.Key.SetPositionAndRotation(entry.Value.Position, entry.Value.Rotation);
                entry.Key.localScale = entry.Value.Scale;
            }

            if (ghosts != null)
                foreach (GameObject ghost in ghosts)
                    if (ghost != null) ghost.SetActive(false);

            if (peekGoblin != null)
            {
                if (haveRest) peekGoblin.position = peekRest;
                peekGoblin.gameObject.SetActive(false);
            }
        }

        void OnTapped(Transform block)
        {
            if (block != null && !pending.Contains(block)) pending.Add(block);
        }

        // ----- The blocks ------------------------------------------------------------------------

        void SendToSlot(Transform block, List<Transform> targets)
        {
            int slotIndex = placed;
            placed++;
            flying++;
            placedBlocks.Add(block);
            targets.Remove(block);
            if (picker != null) picker.Remove(block);

            VoicePlayer.Say("number." + placed, true);
            this.Fork(FlyBlock(block, slotIndex));
        }

        IEnumerator FlyBlock(Transform block, int slotIndex)
        {
            Transform slot = slots[slotIndex];
            Quaternion toRotation = slot.rotation;
            Quaternion fromRotation = block.rotation;
            Vector3 from = block.position;
            Vector3 to = slot.position;

            for (float elapsed = 0f; elapsed < FlySeconds; elapsed += Time.deltaTime)
            {
                if (block == null) { flying--; yield break; }

                float k = elapsed / FlySeconds;
                float eased = Mathf.SmoothStep(0f, 1f, k);
                block.position = Vector3.Lerp(from, to, eased) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 1.2f);
                block.rotation = Quaternion.Slerp(fromRotation, toRotation, eased);
                yield return null;
            }

            if (block != null)
            {
                block.SetPositionAndRotation(to, toRotation);
                Sfx.Play("castle.block_place", to);
                BattleEffects.BlockSpark(to);
            }
            if (ghosts != null && slotIndex < ghosts.Length && ghosts[slotIndex] != null) ghosts[slotIndex].SetActive(false);
            flying--;
        }

        Transform NextFreeBlock(List<Transform> targets)
        {
            foreach (Transform block in targets)
                if (block != null && !placedBlocks.Contains(block)) return block;
            return null;
        }

        // ----- The goblin ------------------------------------------------------------------------

        /// <summary>The goblin behind the hole rises 0.5 m, says its line, giggles, and drops back (then it is hidden if asked).</summary>
        IEnumerator PeekRoutine(string line, float delay, float stay, bool hideAfter)
        {
            if (peekGoblin == null) yield break;

            if (!haveRest)
            {
                peekRest = peekGoblin.position;
                haveRest = true;
            }
            if (delay > 0f) yield return new WaitForSeconds(delay);

            peekGoblin.position = peekRest;
            peekGoblin.gameObject.SetActive(true);
            Vector3 up = peekRest + Vector3.up * PeekHeight;
            yield return CastleMotion.Slide(peekGoblin, up, 0.35f);

            VoicePlayer.Say(line);
            Sfx.Play("battle.giggle", up);
            yield return new WaitForSeconds(stay);

            yield return CastleMotion.Slide(peekGoblin, peekRest, 0.35f);
            if (hideAfter) peekGoblin.gameObject.SetActive(false);
        }

        void CacheHomes()
        {
            if (homes.Count > 0 || pileBlocks == null) return;

            foreach (Transform block in pileBlocks)
            {
                if (block == null) continue;
                homes[block] = new Home { Position = block.position, Rotation = block.rotation, Scale = block.localScale };
            }
        }
    }
}
