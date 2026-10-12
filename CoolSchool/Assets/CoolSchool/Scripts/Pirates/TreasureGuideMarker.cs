using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The thing in the world that shows where to go next on the treasure map: a big arrow that bounces over the landmark (and
    /// then over the X) and a glowing ring on the ground around it. It slides over to the next place when the adventure moves it.
    /// The builder makes the arrow and the ring from primitives (with their glowing materials); the arrow's cone is a mesh made
    /// here, because there is no cone among the primitives. No colliders, so it never gets in his way.
    /// </summary>
    public class TreasureGuideMarker : MonoBehaviour
    {
        [Tooltip("The arrow, pointing down, with its tip at this object's position. Moves up and down.")]
        [SerializeField] Transform arrow;
        [Tooltip("The arrow's cone. Its mesh is made here when it has none.")]
        [SerializeField] MeshFilter arrowHead;
        [Tooltip("The glowing disc on the ground, one metre across. It is scaled to the size of the landmark.")]
        [SerializeField] Transform ring;
        [SerializeField] float bounceHeight = 0.35f;
        [SerializeField] float bounceSpeed = 5f;
        [SerializeField] float slideSeconds = 0.45f;

        const float RingLift = 0.04f;
        const float PopSeconds = 0.35f;
        // The arrow: a cone (tip down) with a shaft on top. The builder sizes the shaft to match: 1.7 m in all.
        public const float ConeRadius = 0.7f;
        public const float ConeHeight = 1f;

        Vector3 groundPoint;
        float arrowBase;
        float ringDiameter = 3f;
        Vector3 slideFrom;
        float slideStart;
        float popStart;
        bool shown;

        void Awake()
        {
            if (arrowHead != null && arrowHead.sharedMesh == null) arrowHead.sharedMesh = MakeCone(ConeRadius, ConeHeight, 20);
        }

        /// <summary>Puts the marker on a landmark: the ring on the ground at the point, the arrow's tip `arrowBase` metres above it.</summary>
        public void Show(Vector3 point, float arrowHeightAbovePoint, float ringRadius)
        {
            slideFrom = shown ? transform.position : point;
            slideStart = Time.time;
            popStart = shown ? popStart : Time.time;

            groundPoint = point;
            arrowBase = arrowHeightAbovePoint;
            ringDiameter = Mathf.Max(0.5f, ringRadius * 2f);
            shown = true;

            gameObject.SetActive(true);
            transform.position = slideFrom;
            Apply();
        }

        public void Hide()
        {
            shown = false;
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (shown) Apply();
        }

        void Apply()
        {
            // The whole marker slides from the last place to the new one; the arrow bounces and turns, the ring breathes.
            float slide = slideSeconds > 0f ? Mathf.Clamp01((Time.time - slideStart) / slideSeconds) : 1f;
            slide = Mathf.SmoothStep(0f, 1f, slide);
            Vector3 position = Vector3.Lerp(slideFrom, groundPoint, slide);
            // A little hop in the middle of the slide, so it is clear it travels.
            position.y += Mathf.Sin(slide * Mathf.PI) * 1.2f;
            transform.position = position;

            float pop = Mathf.Clamp01((Time.time - popStart) / PopSeconds);
            float popScale = pop < 1f ? EaseOutBack(pop) : 1f;
            transform.localScale = Vector3.one * popScale;

            if (arrow != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(Time.time * bounceSpeed)) * bounceHeight;
                arrow.localPosition = new Vector3(0f, arrowBase + bounce, 0f);
                arrow.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            }

            if (ring != null)
            {
                float breathe = 1f + Mathf.Sin(Time.time * 4f) * 0.06f;
                // The ring stays on the ground, whatever hop the marker is in.
                ring.position = new Vector3(position.x, groundPoint.y + RingLift, position.z);
                ring.localScale = new Vector3(ringDiameter * breathe, ring.localScale.y, ringDiameter * breathe);
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

            // Ring of side vertices (own copies per face so the shading is smooth), the tip repeated for each, then the lid.
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
                // Side face, seen from outside (the tip is below).
                triangles[t] = sides + i;
                triangles[t + 1] = next;
                triangles[t + 2] = i;
                // Lid, seen from above.
                triangles[t + 3] = sides * 2;
                triangles[t + 4] = i;
                triangles[t + 5] = next;
            }

            var mesh = new Mesh { name = "GuideArrowCone" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
