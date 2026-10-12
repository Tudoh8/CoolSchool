using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.World
{
    /// <summary>
    /// Makes the front wall of the big castle see-through while the kid is inside the courtyard, so the camera
    /// (which looks north over the front wall) can still see him and the battle. The wall fades back to normal when
    /// he walks out. Only materials change; colliders are never touched.
    /// </summary>
    public class CastleCutaway : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Every renderer of the front wall pieces (towers, panels and gate).")]
        [SerializeField] Renderer[] frontRenderers;
        [Tooltip("An alpha-blended copy of the cardboard material.")]
        [SerializeField] Material ghostMaterial;
        [Tooltip("World space. Only x and z are tested: the inner rectangle of the courtyard.")]
        [SerializeField] Bounds insideArea;
        [SerializeField] float ghostAlpha = 0.22f;
        [SerializeField] float fadeSeconds = 0.35f;

        PlayerController player;
        Material[][] originalMaterials;
        Material runtimeMaterial;
        bool ghosted;
        float alpha = 1f;

        /// <summary>True while the kid stands in the courtyard.</summary>
        public bool PlayerInside { get; private set; }

        void Awake()
        {
            // Remember what every slot looked like, so we can put it back exactly.
            if (frontRenderers == null) frontRenderers = new Renderer[0];
            originalMaterials = new Material[frontRenderers.Length][];
            for (int i = 0; i < frontRenderers.Length; i++)
                originalMaterials[i] = frontRenderers[i] != null ? frontRenderers[i].sharedMaterials : null;
        }

        void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            PlayerInside = player != null && IsInside(player.transform.position);

            float target = PlayerInside ? ghostAlpha : 1f;

            if (!ghosted)
            {
                // Nothing to do while the wall is normal and the kid is outside.
                if (!PlayerInside) return;
                if (!StartGhosting()) return;
            }

            float step = fadeSeconds > 0.0001f ? Time.deltaTime * (1f - ghostAlpha) / fadeSeconds : 1f;
            alpha = Mathf.MoveTowards(alpha, target, step);
            runtimeMaterial.SetColor(BaseColorId, WithAlpha(runtimeMaterial.GetColor(BaseColorId), alpha));

            if (!PlayerInside && alpha >= 1f) StopGhosting();
        }

        bool IsInside(Vector3 position)
        {
            Vector3 min = insideArea.min;
            Vector3 max = insideArea.max;
            return position.x >= min.x && position.x <= max.x && position.z >= min.z && position.z <= max.z;
        }

        /// <summary>Swaps every slot of every front renderer to the one shared ghost instance.</summary>
        bool StartGhosting()
        {
            if (ghostMaterial == null) return false;

            if (runtimeMaterial == null)
            {
                runtimeMaterial = new Material(ghostMaterial) { name = ghostMaterial.name + " (Runtime)" };
                runtimeMaterial.SetColor(BaseColorId, WithAlpha(runtimeMaterial.GetColor(BaseColorId), alpha));
            }

            for (int i = 0; i < frontRenderers.Length; i++)
            {
                Renderer front = frontRenderers[i];
                if (front == null || originalMaterials[i] == null) continue;

                var swapped = new Material[originalMaterials[i].Length];
                for (int slot = 0; slot < swapped.Length; slot++) swapped[slot] = runtimeMaterial;
                front.sharedMaterials = swapped;
            }

            ghosted = true;
            return true;
        }

        void StopGhosting()
        {
            for (int i = 0; i < frontRenderers.Length; i++)
            {
                Renderer front = frontRenderers[i];
                if (front != null && originalMaterials[i] != null) front.sharedMaterials = originalMaterials[i];
            }

            ghosted = false;
            alpha = 1f;
        }

        static Color WithAlpha(Color color, float a)
        {
            color.a = a;
            return color;
        }

        void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
