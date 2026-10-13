using System.Collections;
using System.Collections.Generic;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CoolSchool.Castle
{
    /// <summary>
    /// Naming the baby dragon by the first sound of its name. Letter cards show up; any card is right (it is his choice): the
    /// letter he picks decides the name (m is Mimi, v is Vonkie...). Then the baby becomes his buddy: the plush dragon is unlocked
    /// and shown like a present. This moment never counts a mistake.
    /// </summary>
    public class DragonNaming : MonoBehaviour
    {
        [SerializeField] LessonUI lessonUI;
        [SerializeField] LetterSet letters;
        [SerializeField] BabyDragon baby;
        [SerializeField] RewardCatalog catalog;

        public const string BuddyItemId = "plush.dragon";

        /// <summary>The naming question and what follows. Safe to stop at any moment (then call ResetMoment).</summary>
        public IEnumerator Run(int level, MomentResult result)
        {
            if (lessonUI == null || letters == null) yield break;

            LetterEntry[] cards = BuildCards(level <= 1 ? 2 : 3);
            if (cards.Length == 0) yield break;

            char picked = '\0';
            yield return CardQuestion.AskLetterFrom(lessonUI, cards, '\0', () => VoicePlayer.Say("castle.baby_name"),
                null, c => picked = c, keepCards: true);
            if (picked == '\0') picked = cards[0].Char;

            // His card grows, then the sound and the name.
            lessonUI.CelebrateCard(picked);
            VoicePlayer.Say("lesson.correct", true);
            yield return new WaitForSeconds(0.8f);
            lessonUI.HideCards();
            yield return CastleMotion.WaitForVoice(3f);

            LetterEntry entry = letters.Find(picked);
            if (entry != null) yield return CastleMotion.SayAndWait(CastleMotion.SoundKeyOf(entry));
            VoicePlayer.Say("castle.baby_named." + picked);
            if (baby != null)
            {
                baby.Hop();
                BattleEffects.Confetti(baby.transform.position + Vector3.up * 0.8f);
            }
            yield return CastleMotion.WaitForVoice(5f);

            // The baby is his buddy from now on.
            PlayerProgress progress = ProgressStore.Current;
            progress.babyDragonLetter = picked.ToString();
            ProgressStore.Save();

            RewardItem item = catalog != null ? catalog.Find(BuddyItemId) : null;
            if (item != null && !progress.IsUnlocked(item.id)) RewardSystem.GiveEarnedItem(item);
            yield return CastleMotion.SayAndWait("castle.baby_buddy");
        }

        /// <summary>The cards go away. Safe at any moment.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            if (lessonUI != null && lessonUI.IsShowingCards) lessonUI.HideCards();
        }

        /// <summary>
        /// Letters he has had that have a dragon name, the newest first. With fewer than two, the first letters of the school order
        /// that have a name fill the rest. Look-alikes are kept apart. The cards are shuffled.
        /// </summary>
        LetterEntry[] BuildCards(int cardCount)
        {
            int count = Mathf.Clamp(cardCount, 2, 3);
            var chosen = new List<char>();

            List<string> introduced = ProgressStore.Current.lettersIntroduced;
            if (introduced != null)
            {
                for (int i = introduced.Count - 1; i >= 0 && chosen.Count < count; i--)
                {
                    if (string.IsNullOrEmpty(introduced[i])) continue;

                    char letter = introduced[i][0];
                    if (Usable(letter, chosen)) chosen.Add(letter);
                }
            }

            if (chosen.Count < 2)
            {
                foreach (char letter in LetterCurriculum.SchoolOrder)
                {
                    if (chosen.Count >= count) break;
                    if (Usable(letter, chosen)) chosen.Add(letter);
                }
            }

            var cards = new List<LetterEntry>();
            foreach (char letter in chosen) cards.Add(letters.Find(letter));
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            return cards.ToArray();
        }

        bool Usable(char letter, List<char> chosen)
        {
            if (!DragonNames.Has(letter) || chosen.Contains(letter)) return false;

            LetterEntry entry = letters.Find(letter);
            if (entry == null || !entry.HasStrokes) return false;

            foreach (char other in chosen)
                if (LetterCurriculum.AreSimilar(letter, other)) return false;
            return true;
        }
    }
}
