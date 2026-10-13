using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// A way to see an exercise without playing it. The editor's preview pictures (PreviewCapture) call StageForPreview to put
    /// the props of one exercise on the table at once, as they stand at their busiest moment: no voice, no animation, no waiting.
    /// It uses the same layout code as the real exercises, so a picture shows what the child would see. Nothing here runs in the game.
    /// </summary>
    public partial class NumbersLesson
    {
        // Set while the preview stages props: toys and trays then appear without their pop-in animation.
        bool stagingForPreview;

        /// <summary>
        /// Puts the props of one kind of exercise on the table and the board, with this many toys (or this number on the board).
        /// For the exercises with a basket some toys are already inside, and for the cup game some are already hiding under it.
        /// Returns false when the lesson has nothing to stage it with (no stage, no toys, no basket).
        /// </summary>
        public bool StageForPreview(ExerciseType type, int count)
        {
            if (stage == null || stage.TableSurface == null) return false;

            Countable toy = PreviewToy();
            if (toy == null && NeedsToys(type)) return false;
            if (!HasProps(type)) return false;

            stagingForPreview = true;
            try
            {
                StageProps(type, count, toy);
            }
            finally
            {
                stagingForPreview = false;
            }

            SettleBlackboardForPreview();
            return true;
        }

        void StageProps(ExerciseType type, int count, Countable toy)
        {
            switch (type)
            {
                case ExerciseType.TapAndCount:
                    SpawnToys(toy, count, 0f);
                    NumberAllToys();
                    if (blackboard != null) blackboard.ShowNumber(count);
                    break;
                case ExerciseType.HowMany:
                case ExerciseType.OneMoreOneLess:
                    SpawnToys(toy, count, 0f);
                    break;
                case ExerciseType.FindNumber:
                case ExerciseType.QuickLook:
                    if (blackboard != null) blackboard.ShowDots(count);
                    break;
                case ExerciseType.GiveMe:
                    StageGiveMe(toy, count);
                    break;
                case ExerciseType.Compare:
                    StageCompare(toy, count);
                    break;
                case ExerciseType.SameAmount:
                    StageSameAmount(toy, count);
                    break;
                case ExerciseType.NumberLine:
                    if (blackboard != null) blackboard.ShowNumberLine(new[] { count - 3, count - 2, count - 1, count }, LineLength - 1);
                    break;
                case ExerciseType.CountBack:
                    SpawnToys(toy, count, 0f, allowTrays: false);
                    if (blackboard != null) blackboard.ShowNumber(count);
                    break;
                case ExerciseType.CountOn:
                    StageCountOn(toy, count);
                    break;
                default:
                    StageHidden(toy, count);
                    break;
            }
        }

        /// <summary>The bear if the set has one (the toy that was in the play-test picture), else the first toy.</summary>
        Countable PreviewToy()
        {
            if (countables == null) return null;

            Countable first = null;
            foreach (Countable entry in countables.entries)
            {
                if (entry == null || entry.prefab == null) continue;
                if (entry.id == "bear") return entry;
                if (first == null) first = entry;
            }
            return first;
        }

        /// <summary>Every toy shows its number, as at the end of "tap and count".</summary>
        void NumberAllToys()
        {
            for (int i = 0; i < toys.Count; i++)
                toys[i].ShowNumber(i + 1);
        }

        /// <summary>The toy sits in the next free place in the basket at once. Only the newest toy in the basket shows its number.</summary>
        void PutInBasketForPreview(CountableObject toy, int alreadyIn, bool showNumber)
        {
            basketToys.Add(toy);
            toy.transform.position = BasketSlot(basketToys.Count - 1, toy);
            toy.transform.localScale *= BasketToyScale;

            ClearOtherBasketNumbers(toy);
            if (showNumber) toy.ShowNumber(alreadyIn + basketToys.Count);
        }

        void StageGiveMe(Countable toy, int number)
        {
            int total = Mathf.Min(MaxGiveMeToys, number + (number >= 8 ? 2 : 3));
            PlaceBasket();
            SpawnToys(toy, total, giveMeToyZ, allowTrays: false, centerX: giveMeToysX, maxWidth: giveMeToysWidth);
            basketToys.Clear();

            for (int i = 0; i < number && i < toys.Count; i++)
                PutInBasketForPreview(toys[i], 0, i == number - 1);
        }

        void StageCompare(Countable toy, int count)
        {
            int right = Mathf.Max(1, count - 2);
            ShowTrayPair(0f);
            for (int i = 0; i < count; i++) SpawnTrayToy(toy, 0, i, 0f);
            for (int i = 0; i < right; i++) SpawnTrayToy(toy, 1, i, 0f);
        }

        void StageSameAmount(Countable toy, int count)
        {
            int number = Mathf.Min(count, SameAmountMaxNumber);
            int loose = Mathf.Min(number + SameAmountExtraToys, SameAmountMaxLoose);

            ShowTray(0, SameAmountTrayX, SameAmountTrayZ);
            for (int i = 0; i < number; i++) SpawnTrayToy(toy, 0, i, 0f);

            PlaceBasket(new Vector2(SameAmountBasketX, SameAmountBasketZ));
            basketToys.Clear();

            var looseToys = new List<CountableObject>();
            Vector2[] layout = ToyLayout(loose, SameAmountLooseZ, SameAmountLooseX, SameAmountLooseWidth, SameAmountLooseRowGap);
            for (int i = 0; i < loose; i++)
                looseToys.Add(SpawnToy(toy, stage.TablePoint(layout[i].x, layout[i].y), SameAmountLooseScale, 0f));

            // Half way: some loose toys are in the basket already.
            int inBasket = Mathf.Min(looseToys.Count, Mathf.Max(1, number / 2));
            for (int i = 0; i < inBasket; i++)
                PutInBasketForPreview(looseToys[i], 0, i == inBasket - 1);
        }

        void StageCountOn(Countable toy, int start)
        {
            int extra = CountOnMaxExtra;
            PlaceBasket();
            basketToys.Clear();
            ShowCountOnLabel(start);

            // The floating number moves itself into place every frame in the game, which does not happen here.
            if (countOnLabel != null)
                countOnLabel.transform.position = new Vector3(basketBounds.center.x, basketBounds.max.y + CountOnLabelLift, basketBounds.center.z);

            Vector2[] layout = ToyLayout(extra, basketPosition.y, CountOnToysX, CountOnToysWidth);
            for (int i = 0; i < extra; i++)
                SpawnToy(toy, stage.TablePoint(layout[i].x, layout[i].y), 1f, 0f);

            // The first new toy has just gone in, with its number.
            PutInBasketForPreview(toys[0], start, true);
        }

        void StageHidden(Countable toy, int total)
        {
            int hidden = Mathf.Clamp(total / 2, 1, total - 1);

            Vector2[] layout = ToyLayout(total, 0f, HiddenRowX, HiddenRowWidth);
            for (int i = 0; i < total; i++)
                SpawnToy(toy, stage.TablePoint(layout[i].x, layout[i].y), 1f, 0f);
            if (blackboard != null) blackboard.ShowNumber(total);

            if (hidingCup != null) hidingCup.PlaceAt(stage.TablePoint(HiddenCupX, HiddenCupZ));
            for (int i = total - hidden; i < total; i++)
                toys[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// The chalk writing pops in with a scale animation that does not run outside play mode and may leave it at zero size:
        /// put every part that is still at zero at full size.
        /// </summary>
        void SettleBlackboardForPreview()
        {
            if (blackboard == null) return;

            foreach (RectTransform part in blackboard.Surface.GetComponentsInChildren<RectTransform>(true))
                if (part != blackboard.Surface && part.localScale == Vector3.zero) part.localScale = Vector3.one;

            Canvas.ForceUpdateCanvases();
        }
    }
}
