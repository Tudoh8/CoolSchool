using CoolSchool.Activities;
using CoolSchool.Lessons;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Adds the numbers lesson to the playground scene: the lesson activity with its stage, camera rig and
    /// blackboard writing, the lesson UI on the touch canvas, a basket, the classroom's glowing zone, and an
    /// invitation to it. Finds the school's anchors by name and skips everything (with a warning) when the
    /// school is not in the scene. Called once by the playground builder, after the voice setup.
    /// </summary>
    public static class LessonSetupBuilder
    {
        const string SchoolName = "School";
        const string AnchorsName = "LessonAnchors";
        const string CanvasName = "TouchUI";
        const string FontName = "Inter-Bold";
        const string InviteGroup = "invite.lesson";

        const float ZoneRadius = 1.4f;
        const float BasketWidth = 0.5f;
        const float BlackboardLift = 0.02f;

        static readonly ActivityBeaconBuilder.Style LessonBeacon = new ActivityBeaconBuilder.Style(
            "Lesson", new Color(0.5f, 2f, 0.7f, 1f), new[] { "Book", "Abacus", "Star" });

        /// <summary>The anchors of the school, found by name.</summary>
        sealed class Anchors
        {
            public Transform School;
            public Transform TableSurface;
            public Transform LessonCamera;
            public Transform BlackboardFace;
            public Transform PlayerSeat;
            public Transform ClassroomZone;
        }

        public static void Setup()
        {
            Anchors anchors = FindAnchors();
            if (anchors == null) return;

            GameObject canvas = GameObject.Find(CanvasName);
            if (canvas == null)
            {
                Debug.LogWarning($"CoolSchool: no '{CanvasName}' canvas found, so the numbers lesson was not built.");
                return;
            }

            CountableSet countables = CountableSetSeeder.CreateOrUpdate();
            Font font = FindFont();
            Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            LessonUI ui = BuildLessonUI(canvas.transform, font, round);
            BlackboardDisplay blackboard = BuildBlackboard(anchors, font, round);
            GameObject basket = BuildBasket(anchors);
            NumbersLesson lesson = BuildLesson(anchors, ui, blackboard, countables, basket, font);

            ActivityZone zone = KnightsArenaBuilder.CreateActivityZone("LessonZone", null, anchors.ClassroomZone.position,
                ZoneRadius, lesson, KnightsArenaBuilder.CreateLessonZoneMaterial(), LessonBeacon);
            AddToInviter(zone);
        }

        // ----- Finding the school ----------------------------------------------------------------

        /// <summary>The school and its anchors, or null (with a warning) when something is missing. Never throws.</summary>
        static Anchors FindAnchors()
        {
            GameObject school = GameObject.Find(SchoolName);
            if (school == null)
            {
                Debug.LogWarning("CoolSchool: no 'School' in the scene, so the numbers lesson was skipped.");
                return null;
            }

            Transform root = school.transform;
            var anchors = new Anchors
            {
                School = root,
                TableSurface = root.Find(AnchorsName + "/TableSurface"),
                LessonCamera = root.Find(AnchorsName + "/LessonCamera"),
                BlackboardFace = root.Find(AnchorsName + "/BlackboardFace"),
                PlayerSeat = root.Find(AnchorsName + "/PlayerSeat"),
                ClassroomZone = root.Find(AnchorsName + "/ClassroomZone"),
            };

            string missing = FirstMissing(anchors);
            if (missing != null)
            {
                Debug.LogWarning($"CoolSchool: the school has no '{AnchorsName}/{missing}' anchor, so the numbers lesson was skipped.");
                return null;
            }
            return anchors;
        }

        static string FirstMissing(Anchors anchors)
        {
            if (anchors.TableSurface == null) return "TableSurface";
            if (anchors.LessonCamera == null) return "LessonCamera";
            if (anchors.BlackboardFace == null) return "BlackboardFace";
            if (anchors.PlayerSeat == null) return "PlayerSeat";
            if (anchors.ClassroomZone == null) return "ClassroomZone";
            return null;
        }

        /// <summary>The teacher, found by the name of her component so there is no hard dependency on the school code.</summary>
        static MonoBehaviour FindTeacher(Transform school)
        {
            foreach (MonoBehaviour behaviour in school.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name == "TeacherCharacter") return behaviour;

            Debug.LogWarning("CoolSchool: no TeacherCharacter in the school; the lesson works but the teacher will not wave.");
            return null;
        }

        // ----- UI --------------------------------------------------------------------------------

        /// <summary>A full-screen, touch-transparent holder on the touch canvas. The lesson UI makes its cards at runtime.</summary>
        static LessonUI BuildLessonUI(Transform canvas, Font font, Sprite round)
        {
            var holder = new GameObject("LessonUI", typeof(RectTransform));
            holder.layer = LayerMask.NameToLayer("UI");

            var rect = (RectTransform)holder.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Above the joystick and the button, but below the present popup, which must cover everything.
            Transform popup = canvas.Find("PresentPopup");
            if (popup != null) rect.SetSiblingIndex(popup.GetSiblingIndex());

            var ui = holder.AddComponent<LessonUI>();
            PlaygroundPrototypeBuilder.SetObject(ui, "font", font);
            PlaygroundPrototypeBuilder.SetObject(ui, "cardSprite", round);
            PlaygroundPrototypeBuilder.SetObjectArray(ui, "gameplayHud", FindGameplayHud(canvas));
            return ui;
        }

        /// <summary>The joystick zone and the action button: the parts of the touch UI that make no sense in class.</summary>
        static Object[] FindGameplayHud(Transform canvas)
        {
            var parts = new System.Collections.Generic.List<Object>();
            foreach (string partName in new[] { "JoystickZone", "ActionButton" })
            {
                Transform part = canvas.Find(partName);
                if (part != null) parts.Add(part.gameObject);
                else Debug.LogWarning($"CoolSchool: no '{partName}' on the touch canvas, so it will stay visible during the lesson.");
            }
            return parts.ToArray();
        }

        static Font FindFont()
        {
            foreach (string guid in AssetDatabase.FindAssets(FontName + " t:Font"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != FontName) continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null) return font;
            }

            Debug.LogWarning($"CoolSchool: font '{FontName}' not found; the lesson numbers will use the default font.");
            return null;
        }

        // ----- Blackboard ------------------------------------------------------------------------

        /// <summary>The chalk writing, hung just in front of the blackboard and sized to it when the blackboard model can be found.</summary>
        static BlackboardDisplay BuildBlackboard(Anchors anchors, Font font, Sprite dot)
        {
            var display = new GameObject("BlackboardDisplay");
            display.transform.SetParent(anchors.BlackboardFace, false);
            // The anchor's forward points into the room; the writing must face the room too, so it turns round.
            display.transform.localPosition = new Vector3(0f, 0f, BlackboardLift);
            display.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var board = display.AddComponent<BlackboardDisplay>();
            PlaygroundPrototypeBuilder.SetObject(board, "font", font);
            PlaygroundPrototypeBuilder.SetObject(board, "dotSprite", dot);
            if (TryMeasureBlackboard(anchors.School, out Vector2 size))
                SetVector2(board, "boardSize", size);
            return board;
        }

        /// <summary>Roughly 80% of the width and 70% of the height of the blackboard model, if there is one.</summary>
        static bool TryMeasureBlackboard(Transform school, out Vector2 size)
        {
            size = default;
            foreach (Transform child in school.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("SM_Prop_Blackboard")) continue;
                if (!PlaygroundPrototypeBuilder.TryGetBounds(child.gameObject, out Bounds bounds)) continue;

                float width = Mathf.Max(bounds.size.x, bounds.size.z);
                size = new Vector2(Mathf.Clamp(width * 0.8f, 0.6f, 3f), Mathf.Clamp(bounds.size.y * 0.7f, 0.4f, 1.6f));
                return true;
            }
            return false;
        }

        static void SetVector2(Object target, string field, Vector2 value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;

            property.vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----- Basket ----------------------------------------------------------------------------

        /// <summary>A bucket as the basket, switched off until the "give me" exercise puts it on the table. Null if no model exists.</summary>
        static GameObject BuildBasket(Anchors anchors)
        {
            GameObject prefab = PlaygroundPrototypeBuilder.LoadPrefab(PlaygroundPrototypeBuilder.Kids("SM_Prop_Bucket_01"));
            if (prefab == null) return null;

            GameObject basket = PlaygroundPrototypeBuilder.Spawn(prefab, anchors.School);
            basket.name = "LessonBasket";
            basket.transform.position = anchors.TableSurface.position;

            // A basket on the table should not get in the way of tapping the toys.
            foreach (Collider collider in basket.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            if (PlaygroundPrototypeBuilder.TryGetBounds(basket, out Bounds bounds))
            {
                float width = Mathf.Max(bounds.size.x, bounds.size.z);
                if (width > 0.0001f) basket.transform.localScale *= BasketWidth / width;
            }

            basket.SetActive(false);
            return basket;
        }

        // ----- The lesson ------------------------------------------------------------------------

        static NumbersLesson BuildLesson(Anchors anchors, LessonUI ui, BlackboardDisplay blackboard, CountableSet countables,
            GameObject basket, Font font)
        {
            var root = new GameObject("NumbersLesson");

            var stage = root.AddComponent<LessonStage>();
            PlaygroundPrototypeBuilder.SetObject(stage, "tableSurface", anchors.TableSurface);
            PlaygroundPrototypeBuilder.SetObject(stage, "lessonCamera", anchors.LessonCamera);
            PlaygroundPrototypeBuilder.SetObject(stage, "blackboardFace", anchors.BlackboardFace);
            PlaygroundPrototypeBuilder.SetObject(stage, "playerSeat", anchors.PlayerSeat);
            PlaygroundPrototypeBuilder.SetObject(stage, "teacher", FindTeacher(anchors.School));
            PlaygroundPrototypeBuilder.SetObject(stage, "ui", ui);

            var rig = root.AddComponent<LessonCameraRig>();

            var lesson = root.AddComponent<NumbersLesson>();
            PlaygroundPrototypeBuilder.SetObject(lesson, "stage", stage);
            PlaygroundPrototypeBuilder.SetObject(lesson, "cameraRig", rig);
            PlaygroundPrototypeBuilder.SetObject(lesson, "blackboard", blackboard);
            PlaygroundPrototypeBuilder.SetObject(lesson, "countables", countables);
            PlaygroundPrototypeBuilder.SetObject(lesson, "basket", basket);
            PlaygroundPrototypeBuilder.SetObject(lesson, "numberFont", font);
            PlaygroundPrototypeBuilder.SetInt(lesson, "exercisesPerLesson", 4);
            PlaygroundPrototypeBuilder.SetInt(lesson, "starsPerCorrect", 1);
            PlaygroundPrototypeBuilder.SetInt(lesson, "minStars", 3);
            return lesson;
        }

        // ----- Invitation ------------------------------------------------------------------------

        /// <summary>
        /// Puts the classroom FIRST in the activity inviter's turns, with its own invite line, so the first invitation is
        /// "De bel gaat! Kom maar naar de klas!"; the tag and castle zones follow. Skips quietly if there is no inviter.
        /// </summary>
        static void AddToInviter(ActivityZone zone)
        {
            var inviter = Object.FindFirstObjectByType<ActivityInviter>();
            if (inviter == null)
            {
                Debug.LogWarning("CoolSchool: no ActivityInviter in the scene, so the classroom will not invite the kid.");
                return;
            }

            var serialized = new SerializedObject(inviter);
            SerializedProperty zones = serialized.FindProperty("zones");
            SerializedProperty groups = serialized.FindProperty("inviteGroups");
            if (zones == null || groups == null) return;

            // The groups line up with the zones; pad them first in case an earlier zone was missing.
            while (groups.arraySize < zones.arraySize)
            {
                groups.InsertArrayElementAtIndex(groups.arraySize);
                groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = string.Empty;
            }

            // The first turn goes to index 0, and the groups shift along with the zones.
            zones.InsertArrayElementAtIndex(0);
            zones.GetArrayElementAtIndex(0).objectReferenceValue = zone;
            groups.InsertArrayElementAtIndex(0);
            groups.GetArrayElementAtIndex(0).stringValue = InviteGroup;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
