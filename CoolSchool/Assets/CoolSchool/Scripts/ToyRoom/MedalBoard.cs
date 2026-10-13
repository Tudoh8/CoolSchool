using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.ToyRoom
{
    /// <summary>
    /// The wall board in the Toy Room where the medals hang (a medal is the present after the whole catalog was won). It has
    /// room for 20 (a 5 x 4 grid of spots); more medals are won but not shown. Medals he has already seen hang there; new ones
    /// wait out of sight until PopNewIn() is called (when he walks into the room), and then pop in one by one with a sparkle.
    /// The medal is a model if one is given, otherwise a gold disc on a red and blue ribbon made in code.
    /// </summary>
    public class MedalBoard : MonoBehaviour
    {
        [Tooltip("A medal or trophy model. Empty: a gold disc with a ribbon is made in code.")]
        [SerializeField] GameObject medalPrefab;
        [Tooltip("The 20 places, a 5 x 4 grid, row by row from the top left. Their forward is the way the medals face.")]
        [SerializeField] Transform[] spots;
        [Tooltip("For the medals made in code. Left empty, plain materials are made at run time.")]
        [SerializeField] Material goldMaterial;
        [SerializeField] Material ribbonMaterialA;
        [SerializeField] Material ribbonMaterialB;
        [SerializeField] float medalSize = 0.24f;

        const float PopSeconds = 0.45f;
        const float PopStagger = 0.3f;

        GameObject[] medals = new GameObject[0];
        bool[] popped = new bool[0];
        int total;
        int seen;

        /// <summary>How many spots there are (the most medals that are shown).</summary>
        public int Capacity => spots != null ? spots.Length : 0;

        /// <summary>How many medals have been won and are waiting to pop in.</summary>
        public int PendingCount
        {
            get
            {
                int count = 0;
                for (int i = seen; i < total && i < medals.Length; i++)
                    if (!popped[i]) count++;
                return count;
            }
        }

        /// <summary>
        /// Hangs the medals he has already seen (up to medalsSeen) on the board. The newer ones (medalsSeen up to medals) are made
        /// but kept out of sight until PopNewIn(). Safe to call as often as you like.
        /// </summary>
        public void Refresh(int medalCount, int medalsSeen)
        {
            int capacity = Capacity;
            total = Mathf.Clamp(medalCount, 0, capacity);
            seen = Mathf.Clamp(Mathf.Min(medalsSeen, medalCount), 0, total);

            if (medals.Length != capacity)
            {
                ClearAll();
                medals = new GameObject[capacity];
                popped = new bool[capacity];
            }

            for (int i = 0; i < capacity; i++)
            {
                bool wanted = i < total;
                if (!wanted)
                {
                    if (medals[i] != null)
                    {
                        DestroyNow(medals[i]);
                        medals[i] = null;
                    }
                    popped[i] = false;
                    continue;
                }

                if (medals[i] == null) medals[i] = MakeMedal(spots[i]);
                if (medals[i] == null) continue;

                bool show = i < seen || popped[i];
                if (medals[i].activeSelf != show) medals[i].SetActive(show);
                if (show && medals[i].transform.localScale != Vector3.one) medals[i].transform.localScale = Vector3.one;
            }
        }

        /// <summary>The medals he has not seen yet pop onto the board one after the other, with a sparkle. Returns how many.</summary>
        public int PopNewIn()
        {
            int count = 0;
            for (int i = seen; i < total && i < medals.Length; i++)
            {
                if (medals[i] == null || popped[i]) continue;

                popped[i] = true;
                if (Application.isPlaying) StartCoroutine(PopRoutine(medals[i], count * PopStagger));
                else medals[i].SetActive(true);
                count++;
            }
            return count;
        }

        IEnumerator PopRoutine(GameObject medal, float delay)
        {
            medal.transform.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (medal == null) yield break;

            medal.SetActive(true);
            Sfx.Play("toyroom.sparkle", medal.transform.position);
            BattleEffects.Sparkle(medal.transform.position);

            for (float t = 0f; t < PopSeconds; t += Time.deltaTime)
            {
                if (medal == null) yield break;

                // A little overshoot, like a pop.
                float k = t / PopSeconds;
                float scale = Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
                medal.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            if (medal != null) medal.transform.localScale = Vector3.one;
        }

        // ----- The medal ---------------------------------------------------------------------------

        GameObject MakeMedal(Transform spot)
        {
            if (spot == null) return null;

            var root = new GameObject("Medal");
            root.transform.SetParent(spot, false);
            root.SetActive(false);

            if (medalPrefab != null) BuildFromPrefab(root.transform);
            else BuildFromShapes(root.transform);
            return root;
        }

        void BuildFromPrefab(Transform root)
        {
            GameObject model = Instantiate(medalPrefab, root);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) DestroyNow(collider);

            if (!ToyShelfSlot.TryLocalBounds(model, root, out Bounds bounds)) return;

            float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (biggest > 0.0001f) model.transform.localScale *= medalSize / biggest;
            if (ToyShelfSlot.TryLocalBounds(model, root, out bounds)) model.transform.localPosition -= bounds.center;
        }

        /// <summary>A gold disc (a flat cylinder facing the front) with two ribbon tails above it.</summary>
        void BuildFromShapes(Transform root)
        {
            Material gold = goldMaterial != null ? goldMaterial : PlainMaterial(new Color(1f, 0.78f, 0.15f), 0.8f);
            Material red = ribbonMaterialA != null ? ribbonMaterialA : PlainMaterial(new Color(0.9f, 0.2f, 0.2f), 0.2f);
            Material blue = ribbonMaterialB != null ? ribbonMaterialB : PlainMaterial(new Color(0.2f, 0.4f, 0.9f), 0.2f);

            // The cylinder primitive is 2 m tall along its axis; turned a quarter, its flat face looks along the spot's forward.
            Transform disc = Shape(PrimitiveType.Cylinder, "Disc", root, new Vector3(0f, -medalSize * 0.15f, 0f),
                new Vector3(medalSize, 0.012f, medalSize), gold);
            disc.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Transform inner = Shape(PrimitiveType.Cylinder, "Star", root, new Vector3(0f, -medalSize * 0.15f, 0.008f),
                new Vector3(medalSize * 0.6f, 0.012f, medalSize * 0.6f), PlainMaterial(new Color(1f, 0.93f, 0.5f), 0.9f));
            inner.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Transform left = Shape(PrimitiveType.Cube, "RibbonLeft", root, new Vector3(-medalSize * 0.14f, medalSize * 0.42f, 0.002f),
                new Vector3(medalSize * 0.26f, medalSize * 0.62f, 0.01f), red);
            left.localRotation = Quaternion.Euler(0f, 0f, 14f);
            Transform right = Shape(PrimitiveType.Cube, "RibbonRight", root, new Vector3(medalSize * 0.14f, medalSize * 0.42f, 0.002f),
                new Vector3(medalSize * 0.26f, medalSize * 0.62f, 0.01f), blue);
            right.localRotation = Quaternion.Euler(0f, 0f, -14f);
        }

        static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroyNow(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Only used when the builder did not give a material.</summary>
        static Material PlainMaterial(Color colour, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        void ClearAll()
        {
            foreach (GameObject medal in medals)
                if (medal != null) DestroyNow(medal);
            medals = new GameObject[0];
            popped = new bool[0];
        }

        static void DestroyNow(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
