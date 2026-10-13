using System;
using System.Collections.Generic;
using CoolSchool.Lessons;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The classroom shots of PreviewCapture: the lesson camera's view of the table with the props of each exercise staged at a
    /// busy moment (the biggest count its level asks for, the basket part full, the cup down). Files are lesson_&lt;exercise&gt;.png in
    /// 16:9, and lesson_&lt;exercise&gt;_4x3.png for the busiest basket exercises.
    /// The roof and the door wall are hidden like the interior reveal does while the kid is in the room, the kid stands at his seat
    /// and the teacher at hers. Number cards are drawn as three solid discs where the lesson UI puts them (the touch canvas is a
    /// screen overlay, which a camera render does not draw), so a picture shows what they cover.
    /// The toys' floating numbers face the camera as they do in the game.
    /// Not staged: the chalk letters of the letters lesson (they are drawn by animations), and moving things (hops, flights).
    /// </summary>
    public static partial class PreviewCapture
    {
        static readonly Vector2Int FourByThree = new Vector2Int(1280, 960);

        // The card colours of LessonUI, one per slot: coral, sunshine and sky.
        static readonly Color[] LessonCardColors =
        {
            new Color(1f, 0.45f, 0.4f, 1f),
            new Color(1f, 0.78f, 0.2f, 1f),
            new Color(0.35f, 0.68f, 1f, 1f),
        };

        const string LessonCardName = "PreviewLessonCard";

        static void AddLessonGroups(List<Group> groups)
        {
            // Name, kind, how many (toys, dots or the biggest number), whether number cards are on screen, whether a 4:3 shot is wanted too.
            groups.Add(LessonGroup("tapcount", ExerciseType.TapAndCount, 8, false, false));
            groups.Add(LessonGroup("tapcount_trays", ExerciseType.TapAndCount, 15, false, false));
            groups.Add(LessonGroup("howmany", ExerciseType.HowMany, 10, true, false));
            groups.Add(LessonGroup("findnumber", ExerciseType.FindNumber, 14, true, false));
            groups.Add(LessonGroup("giveme", ExerciseType.GiveMe, 5, false, false));
            groups.Add(LessonGroup("giveme_max", ExerciseType.GiveMe, 10, false, true));
            groups.Add(LessonGroup("quicklook", ExerciseType.QuickLook, 6, true, false));
            groups.Add(LessonGroup("compare", ExerciseType.Compare, 10, false, false));
            groups.Add(LessonGroup("onemore", ExerciseType.OneMoreOneLess, 9, true, false));
            groups.Add(LessonGroup("sameamount", ExerciseType.SameAmount, 8, false, true));
            groups.Add(LessonGroup("numberline", ExerciseType.NumberLine, 20, true, false));
            groups.Add(LessonGroup("countback", ExerciseType.CountBack, 10, false, false));
            groups.Add(LessonGroup("counton", ExerciseType.CountOn, 17, true, false));
            groups.Add(LessonGroup("hidden", ExerciseType.Hidden, 10, true, false));

            // The letters lesson: three word props in a row, with the three letter cards.
            var letters = new Group { Name = "lesson", Stage = () => StageLessonScene(() => FindLessonObject<LettersLesson>().StageForPreview(3)) };
            letters.Shots.Add(new Shot { Name = "letters", Frame = () => LessonView(true) });
            groups.Add(letters);
        }

        /// <summary>One exercise staged on the table: a 16:9 picture, and a 4:3 one when wanted.</summary>
        static Group LessonGroup(string key, ExerciseType type, int count, bool cards, bool alsoFourByThree)
        {
            var group = new Group { Name = "lesson", Stage = () => StageLessonScene(() => FindLessonObject<NumbersLesson>().StageForPreview(type, count)) };
            group.Shots.Add(new Shot { Name = key, Frame = () => LessonView(cards) });
            if (alsoFourByThree)
                group.Shots.Add(new Shot { Name = key + "_4x3", Size = FourByThree, Frame = () => LessonView(cards) });
            return group;
        }

        // ----- Staging --------------------------------------------------------------------------

        static T FindLessonObject<T>() where T : UnityEngine.Object
        {
            T found = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found == null) throw new InvalidOperationException("no " + typeof(T).Name + " in the scene");
            return found;
        }

        /// <summary>The classroom as it is while a lesson runs, then the props of the exercise (stageProps returns false when it could not).</summary>
        static void StageLessonScene(Func<bool> stageProps)
        {
            HideZoneVisuals();
            HideLessonRoofAndDoorWall();
            SeatLessonPeople();

            if (!stageProps()) throw new InvalidOperationException("the lesson could not stage its props (no toys, basket, trays or cup?)");
        }

        /// <summary>What BuildingInteriorReveal does at run time while the kid is in the room: the roof and the door wall are off.</summary>
        static void HideLessonRoofAndDoorWall()
        {
            var reveal = FindLessonObject<CoolSchool.School.BuildingInteriorReveal>();
            var serialized = new SerializedObject(reveal);

            SerializedProperty roof = serialized.FindProperty("hideWhenInside");
            for (int i = 0; roof != null && i < roof.arraySize; i++)
            {
                var renderer = roof.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                if (renderer != null) renderer.enabled = false;
            }

            SerializedProperty walls = serialized.FindProperty("hideWhenCameraOutside");
            for (int i = 0; walls != null && i < walls.arraySize; i++)
            {
                var wall = walls.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (wall != null) wall.SetActive(false);
            }
        }

        /// <summary>The kid at his seat looking at the table, and the teacher in her idle pose.</summary>
        static void SeatLessonPeople()
        {
            LessonStage stage = FindLessonObject<LessonStage>();

            GameObject kid = FindKid("Player");
            if (kid != null && stage.PlayerSeat != null)
            {
                ActivateWithParents(kid);
                DisableMovers(kid);

                Vector3 toTable = stage.TableSurface.position - stage.PlayerSeat.position;
                toTable.y = 0f;
                kid.transform.SetPositionAndRotation(stage.PlayerSeat.position, Quaternion.LookRotation(toTable, Vector3.up));
                ResetAnimators(kid);
                StepAnimators(kid, 0.8f);
            }

            GameObject teacher = GameObject.Find("Teacher");
            if (teacher != null)
            {
                ResetAnimators(teacher);
                StepAnimators(teacher, 0.6f);
            }
            Physics.SyncTransforms();
        }

        // ----- The camera -----------------------------------------------------------------------

        /// <summary>The lesson camera's pose (the LessonCamera anchor), the numbers facing it, and the number cards if they are on screen.</summary>
        static void LessonView(bool cards)
        {
            LessonStage stage = FindLessonObject<LessonStage>();
            Transform anchor = stage.LessonCamera;
            if (anchor == null) throw new InvalidOperationException("the lesson stage has no camera anchor");

            cam.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            cam.farClipPlane = 200f;

            FaceLessonNumbers();
            DrawLessonCards(cards);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>The toys' numbers and the floating digit turn to the camera every frame in the game; here once.</summary>
        static void FaceLessonNumbers()
        {
            foreach (GameObject go in AllSceneObjects())
                if (go.name == "CountNumber" || go.name == "FloatingDigit") go.transform.rotation = cam.transform.rotation;
        }

        /// <summary>
        /// Three solid discs where the LessonUI puts its cards (bottom of the screen, in a 1080 high reference screen scaled by the
        /// height), a little in front of the camera. Old ones are removed first, so every shot gets its own.
        /// </summary>
        static void DrawLessonCards(bool draw)
        {
            foreach (GameObject old in AllSceneObjects())
                if (old.name == LessonCardName) UnityEngine.Object.DestroyImmediate(old);
            if (!draw) return;

            float size = 300f;
            float gap = 80f;
            float bottom = 30f;
            LessonUI ui = UnityEngine.Object.FindFirstObjectByType<LessonUI>(FindObjectsInactive.Include);
            if (ui != null)
            {
                var serialized = new SerializedObject(ui);
                size = ReadLessonFloat(serialized, "cardSize", size);
                gap = ReadLessonFloat(serialized, "cardGap", gap);
                bottom = ReadLessonFloat(serialized, "bottomMargin", bottom);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            const float distance = 0.6f;
            float unit = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / 1080f;
            Transform eye = cam.transform;

            for (int i = 0; i < LessonCardColors.Length; i++)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = LessonCardName;
                UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());

                float across = (i - (LessonCardColors.Length - 1) * 0.5f) * (size + gap) * unit;
                float up = (bottom + size * 0.5f - 540f) * unit;
                disc.transform.SetPositionAndRotation(eye.position + eye.forward * distance + eye.right * across + eye.up * up,
                    Quaternion.FromToRotation(Vector3.up, -eye.forward));
                disc.transform.localScale = new Vector3(size * unit, 0.001f, size * unit);

                var renderer = disc.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(shader) { color = LessonCardColors[i] };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static float ReadLessonFloat(SerializedObject serialized, string field, float fallback)
        {
            SerializedProperty property = serialized.FindProperty(field);
            return property != null && property.propertyType == SerializedPropertyType.Float ? property.floatValue : fallback;
        }
    }
}
