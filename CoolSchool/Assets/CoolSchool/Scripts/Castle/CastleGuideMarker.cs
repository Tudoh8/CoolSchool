using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// Shows where to go in the castle: a gold arrow that bounces over a point and a glowing ring on the ground, like the
    /// pirates' treasure marker. The builder makes the arrow and the ring from primitives (with glowing gold materials); the
    /// arrow's cone is a mesh made here when an arrowHead is given without one. No colliders, so it never gets in his way.
    /// </summary>
    public class CastleGuideMarker : MonoBehaviour
    {
        [Tooltip("The arrow, pointing down, with its tip at this object's position (plus the height). Moves up and down.")]
        [SerializeField] Transform arrow;
        [Tooltip("Optional: the arrow's cone. Its mesh is made here when it has none.")]
        [SerializeField] MeshFilter arrowHead;
        [Tooltip("The glowing disc on the ground. Keeps the size the builder gave it, and breathes a little.")]
        [SerializeField] Transform ring;
        [Tooltip("How high above the point the arrow's tip floats, in metres.")]
        [SerializeField] float arrowHeight = 2.2f;
        [SerializeField] float bounceHeight = 0.35f;
        [SerializeField] float bounceSpeed = 5f;

        const float RingLift = 0.04f;
        const float PopSeconds = 0.35f;
        const float ConeRadius = 0.7f;
        const float ConeHeight = 1f;

        Vector3 groundPoint;
        Vector3 ringBaseScale = Vector3.one;
        bool haveRingScale;
        float popStart;
        bool shown;

        void Awake()
        {
            if (arrowHead != null && arrowHead.sharedMesh == null) arrowHead.sharedMesh = MakeCone(ConeRadius, ConeHeight, 20);
        }

        /// <summary>Puts the marker at a point on the ground: the ring there, the arrow floating above it.</summary>
        public void ShowAt(Vector3 point)
        {
            if (!haveRingScale && ring != null)
            {
                ringBaseScale = ring.localScale;
                haveRingScale = true;
            }

            if (!shown) popStart = Time.time;
            shown = true;
            groundPoint = point;

            gameObject.SetActive(true);
            transform.position = point;
            Apply();
        }

        /// <summary>Takes the marker away.</summary>
        public void Hide()
        {
            shown = false;
            gameObject.SetActive(false);
        }

        /// <summary>For the activity's clean-up: the same as Hide.</summary>
        public void ResetMoment()
        {
            Hide();
        }

        void LateUpdate()
        {
            if (shown) Apply();
        }

        void Apply()
        {
            float pop = Mathf.Clamp01((Time.time - popStart) / PopSeconds);
            float popScale = pop < 1f ? EaseOutBack(pop) : 1f;
            transform.position = groundPoint;
            transform.localScale = Vector3.one * popScale;

            if (arrow != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(Time.time * bounceSpeed)) * bounceHeight;
                arrow.localPosition = new Vector3(0f, arrowHeight + bounce, 0f);
                arrow.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            }

            if (ring != null)
            {
                float breathe = 1f + Mathf.Sin(Time.time * 4f) * 0.06f;
                // The ring stays flat on the ground.
                ring.position = new Vector3(groundPoint.x, groundPoint.y + RingLift, groundPoint.z);
                ring.localScale = new Vector3(ringBaseScale.x * breathe, ringBaseScale.y, ringBaseScale.z * breathe);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        /// <summary>A cone that points down: its tip at the origin, the round top `height` above it.</summary>
        static Mesh MakeCone(float radius, float height, int sides)
        {
            var vertices = new Vector3[sides * 2 + 2];
            var triangles = new int[sides * 6];

            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                vertices[sides + i] = Vector3.zero;
            }
            vertices[sides * 2] = new Vector3(0f, height, 0f);

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int t = i * 6;
                triangles[t] = sides + i;
                triangles[t + 1] = next;
                triangles[t + 2] = i;
                triangles[t + 3] = sides * 2;
                triangles[t + 4] = i;
                triangles[t + 5] = next;
            }

            var mesh = new Mesh { name = "CastleGuideArrowCone" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
