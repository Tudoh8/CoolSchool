using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Everything a lesson needs from the classroom: the anchors (table, camera, blackboard, seat),
    /// the teacher and the lesson UI. The setup builder wires it to the school's anchors.
    /// </summary>
    public class LessonStage : MonoBehaviour
    {
        [Tooltip("Centre of the table top. Its forward axis points toward the camera.")]
        [SerializeField] Transform tableSurface;
        [SerializeField] Transform lessonCamera;
        [SerializeField] Transform blackboardFace;
        [SerializeField] Transform playerSeat;
        [Tooltip("The CoolSchool.School.TeacherCharacter. It only needs a public Wave() method.")]
        [SerializeField] MonoBehaviour teacher;
        [SerializeField] LessonUI ui;

        public Transform TableSurface => tableSurface;
        public Transform LessonCamera => lessonCamera;
        public Transform BlackboardFace => blackboardFace;
        public Transform PlayerSeat => playerSeat;
        public LessonUI UI => ui;

        /// <summary>A point on the table top. X goes along the table, z toward the camera.</summary>
        public Vector3 TablePoint(float x, float z)
        {
            return tableSurface != null ? tableSurface.TransformPoint(new Vector3(x, 0f, z)) : new Vector3(x, 0f, z);
        }

        /// <summary>Which way things on the table face: toward the camera, as the table's forward.</summary>
        public Quaternion TableFacing()
        {
            return tableSurface != null ? Quaternion.LookRotation(tableSurface.forward, Vector3.up) : Quaternion.identity;
        }

        /// <summary>The teacher waves, if there is a teacher who can.</summary>
        public void TeacherWave()
        {
            if (teacher != null) teacher.SendMessage("Wave", SendMessageOptions.DontRequireReceiver);
        }
    }
}
