using CoolSchool.Playground;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>Test menus for the playground toys: fill or empty the sticker collection, and make the sticker board introduce itself again.</summary>
    public static class PlaygroundToysMenu
    {
        [MenuItem("CoolSchool/Playground Toys/Give All Stickers")]
        static void GiveAll()
        {
            ToyStickers.SetAll(true);
            Debug.Log($"PlaygroundToys: all {ToyStickers.AllIds.Length} stickers given (saved).");
        }

        [MenuItem("CoolSchool/Playground Toys/Clear Stickers")]
        static void ClearAll()
        {
            ToyStickers.SetAll(false);
            Debug.Log("PlaygroundToys: all stickers cleared (saved).");
        }

        [MenuItem("CoolSchool/Playground Toys/Clear Board Intro")]
        static void ClearIntro()
        {
            ProgressStore.Current.toyBoardIntroHeard = false;
            ProgressStore.Save();
            Debug.Log("PlaygroundToys: the sticker board will introduce itself again (saved).");
        }
    }
}
