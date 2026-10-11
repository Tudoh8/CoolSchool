using System;
using System.Collections;
using CoolSchool.CameraRig;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Moves the main camera from the follow camera to the lesson viewpoint and back again.
    /// While in the lesson the FollowCamera is switched off.
    /// </summary>
    public class LessonCameraRig : MonoBehaviour
    {
        [Tooltip("Seconds the camera takes to glide in or out.")]
        [SerializeField] float moveSeconds = 0.8f;

        Camera lessonCamera;
        FollowCamera follow;
        bool inLesson;

        public bool InLesson => inLesson;

        /// <summary>Glides the camera to the viewpoint. The follow camera sleeps until ExitLesson.</summary>
        public IEnumerator EnterLesson(Transform viewpoint)
        {
            if (!FindCamera() || viewpoint == null) yield break;

            if (follow != null) follow.enabled = false;
            inLesson = true;

            yield return Glide(() => new Pose(viewpoint.position, viewpoint.rotation));
        }

        /// <summary>Glides back to where the follow camera wants to be, then hands the camera back to it.</summary>
        public IEnumerator ExitLesson()
        {
            if (!inLesson || !FindCamera()) yield break;

            if (follow != null) yield return Glide(follow.DesiredPose);
            RestoreNow();
        }

        /// <summary>Puts the follow camera back at once. Safe to call at any time, e.g. when a lesson is cut short.</summary>
        public void RestoreNow()
        {
            if (!inLesson) return;

            inLesson = false;
            if (follow == null) return;

            follow.enabled = true;
            follow.Snap();
        }

        void OnDisable()
        {
            RestoreNow();
        }

        bool FindCamera()
        {
            if (lessonCamera == null) lessonCamera = Camera.main;
            if (lessonCamera == null) return false;

            if (follow == null) follow = lessonCamera.GetComponent<FollowCamera>();
            return true;
        }

        /// <summary>Eases the camera from where it is to a pose that may keep moving (the target is asked every frame).</summary>
        IEnumerator Glide(Func<Pose> target)
        {
            Transform cam = lessonCamera.transform;
            Vector3 startPosition = cam.position;
            Quaternion startRotation = cam.rotation;
            float duration = Mathf.Max(0.05f, moveSeconds);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                Pose pose = target();
                cam.SetPositionAndRotation(Vector3.Lerp(startPosition, pose.position, k), Quaternion.Slerp(startRotation, pose.rotation, k));
                yield return null;
            }

            Pose end = target();
            cam.SetPositionAndRotation(end.position, end.rotation);
        }
    }
}
