using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Activities
{
    /// <summary>
    /// A glowing spot on the ground. Walking into it starts an activity.
    /// It only starts again after the kid has walked out of the circle, so it never loops.
    /// </summary>
    public class ActivityZone : MonoBehaviour
    {
        [SerializeField] Activity activity;
        [SerializeField] float radius = 1.5f;
        [Tooltip("The glowing circle. Hidden while the activity runs and gently pulsing otherwise.")]
        [SerializeField] GameObject visual;

        [Header("Pulse")]
        [SerializeField] float pulseAmount = 0.06f;
        [SerializeField] float pulseSpeed = 2.5f;

        PlayerController player;
        Vector3 visualBaseScale = Vector3.one;
        // Starts false so a kid who begins the scene standing in a zone does not start it by accident.
        bool armed;

        public Activity Activity => activity;

        public float Radius => radius;

        void Awake()
        {
            if (visual != null) visualBaseScale = visual.transform.localScale;
        }

        void Update()
        {
            if (activity == null) return;

            ActivityManager manager = ActivityManager.Instance;
            if (manager == null) return;

            if (player == null) player = FindAnyObjectByType<PlayerController>();

            bool idle = !activity.IsRunning && manager.Current == manager.DefaultActivity;
            UpdateVisual(idle);

            if (player == null) return;

            if (!IsPlayerInside())
            {
                armed = true;
                return;
            }

            if (armed && idle)
            {
                armed = false;
                manager.StartActivity(activity);
            }
        }

        void OnDisable()
        {
            if (visual != null) visual.transform.localScale = visualBaseScale;
        }

        bool IsPlayerInside()
        {
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        void UpdateVisual(bool idle)
        {
            if (visual == null) return;

            // Hidden while this zone's activity (or any other one) is running.
            if (visual.activeSelf != idle) visual.SetActive(idle);
            if (!idle) return;

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            visual.transform.localScale = visualBaseScale * pulse;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
