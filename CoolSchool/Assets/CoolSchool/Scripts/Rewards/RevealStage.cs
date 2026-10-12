using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// A tiny stage far below the playground where the won item spins slowly. A camera that only sees
    /// the stage's layer films it into a RenderTexture, which the present popup shows on screen.
    /// The main camera is told to ignore the stage's layer, so the item is never seen in the world.
    /// </summary>
    public class RevealStage : MonoBehaviour
    {
        [Tooltip("Film the stage into a RenderTexture. Kept switched off until an item is shown.")]
        [SerializeField] Camera revealCamera;
        [Tooltip("The middle of the stage, where the item is placed.")]
        [SerializeField] Transform pivot;
        [Tooltip("The layer used by the stage. The reveal camera sees only this layer and the main camera ignores it.")]
        [SerializeField] int stageLayer = 31;
        [Tooltip("The biggest side of the item is scaled to this many metres.")]
        [SerializeField] float fitSize = 1.8f;
        [SerializeField] float spinDegreesPerSecond = 40f;
        [Tooltip("Swords and other long things that lie down are stood up.")]
        [SerializeField] bool standLongThingsUp = true;

        const float LongThingRatio = 1.8f;

        Transform spinner;

        void Awake()
        {
            if (revealCamera != null)
            {
                revealCamera.cullingMask = 1 << stageLayer;
                revealCamera.enabled = false;
            }

            Camera main = Camera.main;
            if (main != null) main.cullingMask &= ~(1 << stageLayer);
        }

        /// <summary>Puts the item on the stage and starts filming. Returns false if the item has no model.</summary>
        public bool Show(RewardItem item)
        {
            Hide();
            if (item == null || item.prefab == null || pivot == null) return false;

            spinner = new GameObject("Spinner").transform;
            spinner.SetParent(pivot, false);

            Transform model = Instantiate(item.prefab, spinner).transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.Euler(item.revealEuler);
            PrepareModel(model.gameObject);
            if (item.IsGold) GoldPaint.Apply(model.gameObject);

            // Carts are long and low too, but they stay on their wheels.
            if (standLongThingsUp && item.kind != RewardKind.Cart) StandUp(model);
            FitAndCenter(model, item.revealScale);

            if (revealCamera != null) revealCamera.enabled = true;
            return true;
        }

        public void Hide()
        {
            if (revealCamera != null) revealCamera.enabled = false;
            if (spinner != null) Destroy(spinner.gameObject);
            spinner = null;
        }

        void Update()
        {
            if (spinner != null) spinner.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        void OnDisable()
        {
            Hide();
        }

        /// <summary>Puts the model on the stage's layer and removes anything that could get in the way.</summary>
        void PrepareModel(GameObject model)
        {
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Destroy(collider);

            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = stageLayer;
        }

        /// <summary>A model that lies on its side (longer than tall) is turned so its long side points up.</summary>
        static void StandUp(Transform model)
        {
            if (!TryGetBounds(model.gameObject, out Bounds bounds)) return;

            Vector3 size = bounds.size;
            if (size.x > size.y * LongThingRatio && size.x >= size.z)
                model.rotation = Quaternion.Euler(0f, 0f, 90f) * model.rotation;
            else if (size.z > size.y * LongThingRatio)
                model.rotation = Quaternion.Euler(90f, 0f, 0f) * model.rotation;
        }

        /// <summary>Scales the model to fitSize and moves it so the middle of its bounds is at the pivot.</summary>
        void FitAndCenter(Transform model, float extraScale)
        {
            if (!TryGetBounds(model.gameObject, out Bounds bounds)) return;

            float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (biggest > 0.0001f)
                model.localScale *= fitSize * Mathf.Max(0.1f, extraScale) / biggest;

            if (TryGetBounds(model.gameObject, out bounds))
                model.position += pivot.position - bounds.center;
        }

        static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }
    }
}
