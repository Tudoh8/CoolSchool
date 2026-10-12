using System.Collections.Generic;
using System.Linq;
using CoolSchool.Race;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;
using Kind = CoolSchool.EditorTools.SoapboxTrackLayout.Kind;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The crossing animals (polyperfect Low Poly Animated Animals): unpacked, their wander scripts and physics stripped, their
    /// Built-in Standard materials copied to URP/Lit. The vendor assets are never touched.
    /// </summary>
    internal static partial class SoapboxStreetBuilder
    {
        const string AnimalMaterialFolder = MaterialFolder + "/Animals";

        static readonly PrefabRef CatPrefab = new PrefabRef("Cat_Orange", "polyperfect");
        static readonly PrefabRef DogPrefab = new PrefabRef("Dog_GoldenRetriever", "polyperfect");
        static readonly PrefabRef HenPrefab = new PrefabRef("Hen", "polyperfect");
        static readonly PrefabRef ChickPrefab = new PrefabRef("Chick", "polyperfect");
        static readonly PrefabRef RabbitPrefab = new PrefabRef("Rabbit_White", "polyperfect");

        static readonly Dictionary<Material, Material> AnimalMaterials = new Dictionary<Material, Material>();
        static readonly HashSet<string> UsedAnimalMaterialPaths = new HashSet<string>();

        /// <summary>One AnimalCrosser per animal entry in the table (plus the hen's three chicks). Returns how many animals were made.</summary>
        static int BuildAnimals(Transform itemsParent, Transform pointsParent, Transform followersParent)
        {
            AnimalMaterials.Clear();
            UsedAnimalMaterialPaths.Clear();
            int made = 0;
            foreach (SoapboxTrackLayout.Entry e in SoapboxTrackLayout.Entries.Where(x => x.IsAnimal))
            {
                string kind;
                PrefabRef prefab;
                float height;
                switch (e.Kind)
                {
                    case Kind.Cat: kind = "cat"; prefab = CatPrefab; height = 0.45f; break;
                    case Kind.Dog: kind = "dog"; prefab = DogPrefab; height = 0.7f; break;
                    case Kind.Hen: kind = "hen"; prefab = HenPrefab; height = 0.45f; break;
                    default: kind = "rabbit"; prefab = RabbitPrefab; height = 0.35f; break;
                }

                // Waits on the sidewalk, a little off its middle toward the road (the hen's chicks stand behind her).
                float sign = e.West ? 1f : -1f;
                float sidewalkX = (e.West ? WestSidewalkX : EastSidewalkX) + sign * 0.45f;
                Vector3 home = new Vector3(sidewalkX, SidewalkY, ZAt(e.Distance));
                Quaternion toStreet = Quaternion.LookRotation(e.West ? Vector3.right : Vector3.left, Vector3.up);

                Transform point = NewPoint("Sidewalk_" + kind, pointsParent, home);
                point.rotation = toStreet;

                var root = new GameObject("Animal_" + kind);
                root.transform.SetParent(itemsParent, false);
                root.transform.SetPositionAndRotation(home, toStreet);

                GameObject model = MakeAnimal(prefab, root.transform, height, home, out AudioClip sound);
                if (model == null) continue;

                Transform[] followers = null;
                if (e.Kind == Kind.Hen)
                {
                    var chicks = new List<Transform>();
                    for (int i = 0; i < 3; i++)
                    {
                        // Behind the hen, away from the road, a little to the side of each other.
                        Vector3 chickHome = home + new Vector3(-sign * (0.4f + i * 0.35f), 0f, (i - 1) * 0.25f);
                        var chickRoot = new GameObject("Chick_" + (i + 1));
                        chickRoot.transform.SetParent(followersParent, false);
                        chickRoot.transform.SetPositionAndRotation(chickHome, toStreet);
                        if (MakeAnimal(ChickPrefab, chickRoot.transform, 0.18f, chickHome, out _) != null) chicks.Add(chickRoot.transform);
                    }
                    followers = chicks.ToArray();
                }

                var crosser = root.AddComponent<AnimalCrosser>();
                SetItemBase(crosser, e, 1.2f);
                SetObject(crosser, "animator", model.GetComponentInChildren<Animator>());
                SetObject(crosser, "sound", sound);
                SetString(crosser, "kind", kind);
                SetObject(crosser, "sidewalkPoint", point);
                if (followers != null) SetObjectArray(crosser, "followers", followers);
                SetFloat(crosser, "triggerDistance", 30f);
                SetFloat(crosser, "walkSpeed", 1.4f);
                made++;
            }
            return made;
        }

        /// <summary>
        /// One animal under the parent: unpacked, stripped of everything that is not a mesh or the Animator, with URP materials, scaled
        /// to the wanted height and standing on the sidewalk at the point. Returns null when the prefab is missing.
        /// </summary>
        static GameObject MakeAnimal(PrefabRef reference, Transform parent, float height, Vector3 point, out AudioClip sound)
        {
            sound = null;
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            var animal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(animal, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            animal.name = prefab.name;
            animal.transform.localPosition = Vector3.zero;
            animal.transform.localRotation = Quaternion.identity;
            animal.transform.localScale = Vector3.one;

            sound = ReadAnimalSound(animal);

            // Scripts first (they require the agent, the audio source, and so on), then the rest.
            foreach (MonoBehaviour script in animal.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) Object.DestroyImmediate(script);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(animal);
            foreach (UnityEngine.AI.NavMeshAgent agent in animal.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) Object.DestroyImmediate(agent);
            foreach (Rigidbody body in animal.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (CharacterController controller in animal.GetComponentsInChildren<CharacterController>(true)) Object.DestroyImmediate(controller);
            foreach (Collider collider in animal.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (AudioSource source in animal.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(source);

            // Some animals (the cat) come out of the model importer with the default material, because the importer does not
            // import materials and finds none by name. Those get the vendor's own material of the same name as the prefab.
            Material own = FindVendorMaterial(prefab.name);
            foreach (Renderer renderer in animal.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                var copies = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    Material source = shared[i];
                    string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
                    bool vendors = sourcePath.Contains("polyperfect");
                    Debug.Log($"CoolSchool race street: {prefab.name} slot {i} on {renderer.name}: material '{(source != null ? source.name : "none")}', "
                        + $"shader '{(source != null && source.shader != null ? source.shader.name : "none")}', from '{(string.IsNullOrEmpty(sourcePath) ? "(built in)" : sourcePath)}'.");
                    if (own != null && !vendors)
                    {
                        Debug.Log($"CoolSchool race street: {prefab.name} slot {i} has no vendor material; using {AssetDatabase.GetAssetPath(own)}.");
                        source = own;
                    }
                    copies[i] = UrpCopy(source);
                }
                renderer.sharedMaterials = copies;
            }

            Animator animator = animal.GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;

            if (TryGetBounds(animal, out Bounds bounds) && bounds.size.y > 0.01f)
                animal.transform.localScale *= height / bounds.size.y;
            AlignToBounds(animal, point.x, point.z, point.y, false);
            return animal;
        }

        /// <summary>The vendor material with exactly this name (Cat_Orange.mat for the Cat_Orange prefab), or null.</summary>
        static Material FindVendorMaterial(string materialName)
        {
            foreach (string guid in AssetDatabase.FindAssets(materialName + " t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("polyperfect") || System.IO.Path.GetFileNameWithoutExtension(path) != materialName) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) return material;
            }
            return null;
        }

        /// <summary>The call of the animal: the "animalSound" clip of whichever script on the prefab has one.</summary>
        static AudioClip ReadAnimalSound(GameObject animal)
        {
            foreach (MonoBehaviour script in animal.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null) continue;
                SerializedProperty property = new SerializedObject(script).FindProperty("animalSound");
                if (property != null && property.objectReferenceValue is AudioClip clip) return clip;
            }
            return null;
        }

        /// <summary>
        /// A URP/Lit copy of a Built-in Standard material (_MainTex to _BaseMap, _Color to _BaseColor, low smoothness), saved under
        /// Materials/Race/Animals. Materials that are already URP are used as they are.
        /// </summary>
        static Material UrpCopy(Material source)
        {
            if (source == null) return null;
            if (AnimalMaterials.TryGetValue(source, out Material done)) return done;

            if (source.shader != null && source.shader.name.StartsWith("Universal Render Pipeline"))
            {
                AnimalMaterials[source] = source;
                return source;
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return source;

            var copy = new Material(lit) { name = source.name + "_URP" };
            // Standard, Standard (Specular setup), Legacy and Mobile shaders all call their albedo _MainTex and tint _Color.
            if (source.HasProperty("_MainTex")) copy.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            if (source.HasProperty("_Color")) copy.SetColor("_BaseColor", source.GetColor("_Color"));
            Debug.Log($"CoolSchool race street: converted material '{source.name}' (shader '{(source.shader != null ? source.shader.name : "none")}', "
                + $"texture {(source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null ? source.GetTexture("_MainTex").name : "none")}) to URP/Lit.");
            copy.SetFloat("_Smoothness", 0.1f);
            copy.SetFloat("_Metallic", 0f);

            // Two sources with the same name in one build get a number; an old copy from an earlier build is replaced.
            string path = AnimalMaterialFolder + "/" + copy.name + ".mat";
            int suffix = 1;
            while (UsedAnimalMaterialPaths.Contains(path))
                path = AnimalMaterialFolder + "/" + copy.name + "_" + suffix++ + ".mat";
            UsedAnimalMaterialPaths.Add(path);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            Material saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            AnimalMaterials[source] = saved;
            return saved;
        }
    }
}
