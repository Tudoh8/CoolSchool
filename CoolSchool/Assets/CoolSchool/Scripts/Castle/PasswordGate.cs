using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The password at the gate. Three little cardboard stands rise from the ground, each with a picture on top; the queen says
    /// "tap the picture that starts with..." and a letter sound. The right picture spins and the gate opens. A wrong picture only
    /// wobbles and the sound is said again; the right one starts to bob after two misses or 15 s, and after 45 s it is solved
    /// for him. The first time ever (no letter known) the password teaches the first letter of his name and counts no mistake.
    /// </summary>
    public class PasswordGate : MonoBehaviour
    {
        [Tooltip("The 3 stands in front of the gate, each with a child called PropSpot on top.")]
        [SerializeField] Transform[] stands;
        [SerializeField] WorldTapPicker picker;
        [SerializeField] CastleQueen queen;

        const float PropSide = 0.45f;
        const float RiseSeconds = 0.4f;
        const float RiseDepth = 0.8f;
        const float HintAfterSeconds = 15f;
        const float SolveAfterSeconds = 45f;
        const int MissesBeforeHint = 2;

        static char lastTarget;

        Vector3[] standHome;
        readonly List<GameObject> props = new List<GameObject>();
        Transform tappedProp;
        Transform pulsing;
        Vector3 pulsingBaseScale;

        /// <summary>Asks the password. Says castle.password_right itself when it is done. Safe to stop at any moment (then call ResetMoment).</summary>
        public IEnumerator Run(int level, LetterSet letters, MomentResult result)
        {
            if (letters == null || stands == null || stands.Length == 0) yield break;

            CacheHome();
            LetterEntry target = ChooseTarget(letters, out bool teaching);
            if (target == null) yield break;

            lastTarget = target.Char;
            int wanted = level <= 1 ? 2 : 3;
            LetterWord right = letters.PickWord(target.Char);
            if (right == null) yield break;

            var words = new List<LetterWord> { right };
            foreach (LetterWord other in PickOtherWords(letters, target.Char, wanted - 1))
                words.Add(other);
            if (words.Count < 2) yield break;
            if (words.Count > stands.Length) words.RemoveRange(stands.Length, words.Count - stands.Length);
            Shuffle(words);

            // The stands that are used: the outer ones when there are only two.
            int[] slots = words.Count >= stands.Length ? Sequence(stands.Length) : new[] { 0, stands.Length - 1 };
            int count = Mathf.Min(words.Count, slots.Length);
            var used = new List<Transform>();
            for (int i = 0; i < count; i++) used.Add(stands[slots[i]]);

            // The stands rise, each with its picture.
            for (int i = 0; i < count; i++)
            {
                SpawnProp(words[i], used[i]);
                this.Fork(Rise(used[i], true));
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(RiseSeconds);

            Transform rightProp = props[words.IndexOf(right)].transform;
            string soundKey = CastleMotion.SoundKeyOf(target);

            if (teaching)
            {
                yield return CastleMotion.SayAndWait("castle.password_new");
                yield return CastleMotion.SayAndWait(soundKey);
            }
            yield return CastleMotion.SayAndWait("castle.password");
            VoicePlayer.Say(soundKey);

            // Waiting for a tap.
            var propList = new List<Transform>();
            foreach (GameObject prop in props) propList.Add(prop.transform);
            if (picker != null)
            {
                picker.SetTargets(propList);
                picker.Tapped += OnTapped;
                picker.Armed = true;
            }

            tappedProp = null;
            int misses = 0;
            float lastActivity = Time.time;
            float started = Time.time;
            bool solved = false;
            while (!solved)
            {
                if (tappedProp != null)
                {
                    Transform tapped = tappedProp;
                    tappedProp = null;
                    lastActivity = Time.time;
                    int index = propList.IndexOf(tapped);
                    LetterWord tappedWord = index >= 0 ? words[index] : null;

                    if (picker != null) picker.Armed = false;
                    yield return CastleMotion.Hop(tapped, 0.25f, 0.3f);
                    if (tappedWord != null && !string.IsNullOrEmpty(tappedWord.voiceKey)) yield return CastleMotion.SayAndWait(tappedWord.voiceKey, true);

                    if (tapped == rightProp)
                    {
                        solved = true;
                    }
                    else
                    {
                        // Never a failure: a wobble and the sound again.
                        if (!teaching && result != null) result.Mistakes++;
                        misses++;
                        yield return CastleMotion.Wobble(tapped, 0.5f, 18f);
                        yield return CastleMotion.SayAndWait("lesson.try_again", true);
                        VoicePlayer.Say(soundKey);
                        if (misses >= MissesBeforeHint) StartPulse(rightProp);
                        lastActivity = Time.time;
                        tappedProp = null;
                        if (picker != null) picker.Armed = true;
                    }
                }
                else
                {
                    if (pulsing == null && Time.time - lastActivity >= HintAfterSeconds)
                    {
                        StartPulse(rightProp);
                        if (CastleMotion.VoiceIsFree()) VoicePlayer.Say(soundKey);
                    }
                    if (Time.time - started >= SolveAfterSeconds) solved = true;
                }

                UpdatePulse();
                yield return null;
            }

            StopPulse();
            if (picker != null)
            {
                picker.Armed = false;
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            // The right picture spins with confetti, the gate is open.
            BattleEffects.Confetti(CastleMotion.TopOf(rightProp));
            Sfx.Play("ui.card_right", rightProp.position);
            this.Fork(CastleMotion.SpinHop(rightProp, 0.35f, 0.9f));
            if (queen != null) queen.Cheer();
            VoicePlayer.Say("castle.password_right", true);
            yield return new WaitForSeconds(0.9f);
            yield return CastleMotion.WaitForVoice(6f);

            // The stands sink again.
            foreach (Transform stand in used) this.Fork(Rise(stand, false));
            yield return new WaitForSeconds(RiseSeconds + 0.1f);
            ClearProps();
        }

        /// <summary>Puts the stands back under the ground and away, drops the pictures, lets go of the picker.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            if (picker != null)
            {
                picker.Tapped -= OnTapped;
                picker.Clear();
            }

            pulsing = null;
            tappedProp = null;
            ClearProps();
            if (stands != null && standHome != null)
            {
                for (int i = 0; i < stands.Length && i < standHome.Length; i++)
                {
                    if (stands[i] == null) continue;

                    stands[i].position = standHome[i];
                    stands[i].gameObject.SetActive(false);
                }
            }
        }

        void OnTapped(Transform tapped)
        {
            tappedProp = tapped;
        }

        // ----- Choosing the picture --------------------------------------------------------------

        /// <summary>
        /// A letter he has had that has words: his name's first letter half of the time, else any of them (not the same twice if
        /// there is a choice). With none known yet it is the first letter of his name, taught on the spot.
        /// </summary>
        static LetterEntry ChooseTarget(LetterSet letters, out bool teaching)
        {
            teaching = false;
            PlayerProgress progress = ProgressStore.Current;

            var known = new List<char>();
            if (progress.lettersIntroduced != null)
            {
                foreach (string text in progress.lettersIntroduced)
                {
                    if (string.IsNullOrEmpty(text) || known.Contains(text[0])) continue;

                    LetterEntry entry = letters.Find(text[0]);
                    if (entry != null && entry.HasWords) known.Add(text[0]);
                }
            }

            if (known.Count > 0)
            {
                string name = ProgressStore.NormalizeChildName(progress.childName);
                char pick;
                if (name.Length > 0 && known.Contains(name[0]) && Random.value < 0.5f)
                {
                    pick = name[0];
                }
                else
                {
                    pick = known[Random.Range(0, known.Count)];
                    if (known.Count > 1 && pick == lastTarget) pick = known[(known.IndexOf(pick) + 1) % known.Count];
                }
                return letters.Find(pick);
            }

            // Nothing introduced yet: this teaches the first letter of his name (the classroom introduces it for real later).
            foreach (char c in LetterCurriculum.OrderFor(progress.childName))
            {
                LetterEntry entry = letters.Find(c);
                if (entry != null && entry.HasWords)
                {
                    teaching = true;
                    return entry;
                }
            }
            return null;
        }

        /// <summary>Words from other letters, each with a different first sound. Letters he knows are preferred.</summary>
        static List<LetterWord> PickOtherWords(LetterSet letters, char not, int count)
        {
            var candidates = new List<LetterEntry>();
            foreach (LetterEntry entry in letters.letters)
                if (entry != null && entry.HasWords && LetterCurriculum.SoundGroup(entry.Char) != LetterCurriculum.SoundGroup(not)) candidates.Add(entry);
            Shuffle(candidates);

            var known = new List<char>();
            foreach (string text in ProgressStore.Current.lettersIntroduced ?? new List<string>())
                if (!string.IsNullOrEmpty(text)) known.Add(text[0]);

            var sorted = new List<LetterEntry>();
            foreach (LetterEntry entry in candidates) if (known.Contains(entry.Char)) sorted.Add(entry);
            foreach (LetterEntry entry in candidates) if (!known.Contains(entry.Char)) sorted.Add(entry);

            var words = new List<LetterWord>();
            var usedSounds = new List<char>();
            foreach (LetterEntry entry in sorted)
            {
                if (words.Count >= count) break;

                char sound = LetterCurriculum.SoundGroup(entry.Char);
                if (usedSounds.Contains(sound)) continue;

                LetterWord word = letters.PickWord(entry.Char);
                if (word == null) continue;

                words.Add(word);
                usedSounds.Add(sound);
            }
            return words;
        }

        // ----- The props and stands --------------------------------------------------------------

        void CacheHome()
        {
            if (standHome != null) return;

            standHome = new Vector3[stands.Length];
            for (int i = 0; i < stands.Length; i++)
                standHome[i] = stands[i] != null ? stands[i].position : Vector3.zero;
        }

        /// <summary>Makes the picture on the stand's PropSpot, 0.45 m on its biggest side, standing on top.</summary>
        void SpawnProp(LetterWord word, Transform stand)
        {
            // The stand is put at its place for a moment so the picture can be measured on top of it; Rise then sinks it for the show.
            stand.position = StandHomeOf(stand);
            stand.gameObject.SetActive(true);

            Transform spot = stand.Find("PropSpot");
            Vector3 point = spot != null ? spot.position : stand.position + Vector3.up * 0.6f;

            GameObject instance = Instantiate(word.prefab, stand);
            instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, word.scale);
            instance.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            CastleMotion.StripColliders(instance);
            CastleMotion.FitAndStand(instance.transform, point, PropSide);
            props.Add(instance);
        }

        Vector3 StandHomeOf(Transform stand)
        {
            for (int i = 0; i < stands.Length; i++)
                if (stands[i] == stand && standHome != null) return standHome[i];
            return stand.position;
        }

        /// <summary>The stand rises out of the ground with a sparkle (up), or sinks back into it (down).</summary>
        IEnumerator Rise(Transform stand, bool up)
        {
            if (stand == null) yield break;

            Vector3 home = StandHomeOf(stand);
            Vector3 low = home + Vector3.down * RiseDepth;
            if (up)
            {
                stand.position = low;
                stand.gameObject.SetActive(true);
                Sfx.Play("castle.ladder_appear", home);
                BattleEffects.Sparkle(home + Vector3.up * 0.3f);
            }

            Vector3 from = stand.position;
            Vector3 to = up ? home : low;
            for (float elapsed = 0f; elapsed < RiseSeconds; elapsed += Time.deltaTime)
            {
                if (stand == null) yield break;

                stand.position = Vector3.Lerp(from, to, LessonMotion.OutBack(elapsed / RiseSeconds));
                yield return null;
            }

            if (stand == null) yield break;
            stand.position = to;
            if (!up) stand.gameObject.SetActive(false);
        }

        void ClearProps()
        {
            foreach (GameObject prop in props)
                if (prop != null) Destroy(prop);
            props.Clear();
        }

        // ----- The hint --------------------------------------------------------------------------

        void StartPulse(Transform prop)
        {
            if (prop == null) return;

            pulsing = prop;
            pulsingBaseScale = prop.localScale;
        }

        void UpdatePulse()
        {
            if (pulsing == null) return;

            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            pulsing.localScale = pulsingBaseScale * (1f + beat * 0.14f);
        }

        void StopPulse()
        {
            if (pulsing != null) pulsing.localScale = pulsingBaseScale;
            pulsing = null;
        }

        static int[] Sequence(int count)
        {
            var result = new int[count];
            for (int i = 0; i < count; i++) result[i] = i;
            return result;
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
