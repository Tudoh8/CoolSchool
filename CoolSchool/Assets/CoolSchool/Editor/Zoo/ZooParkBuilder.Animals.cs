using System.Collections.Generic;
using CoolSchool.Characters;
using CoolSchool.Zoo;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The ten zoo animals, the three surprise critters (polyperfect Low Poly Animated Animals: unpacked, their wander scripts and
    /// physics stripped, their Built-in Standard materials copied to URP/Lit under Materials/Zoo/Animals; the vendor assets are never
    /// touched) and the zookeeper (a classmate in an explorer outfit with a scout hat, built like the knight allies).
    /// </summary>
    internal static partial class ZooParkBuilder
    {
        static readonly PrefabRef KeeperPrefab = Kids("SM_Chr_Kid_Explorer_01");
        static readonly PrefabRef KeeperFallbackPrefab = Kids("SM_Chr_Kid_Scout_Shorts_01");
        static readonly PrefabRef ScoutHatPrefab = Kids("SM_Chr_Attach_ScoutHat_01");

        static readonly Dictionary<Material, Material> AnimalMaterials = new Dictionary<Material, Material>();
        static readonly HashSet<string> UsedAnimalMaterialPaths = new HashSet<string>();

        /// <summary>The prefab name, the height in metres and the id of each surprise critter, in the order of HideAndSeekGame.critters.</summary>
        static readonly (string id, string prefab, float height)[] CritterTable =
        {
            ("cat", "Cat_Orange", 0.45f),
            ("rabbit", "Rabbit_White", 0.35f),
            ("squirrel", "Squirrel", 0.3f),
        };

        /// <summary>The result of creating the animals: the components, and the model each one wraps (for the icons).</summary>
        internal sealed class AnimalSet
        {
            public ZooAnimal[] Animals;
            public GameObject[] Models;
        }

        // ---- The ten animals ----

        /// <summary>One "ZooAnimal_&lt;id&gt;" root per kind (ZooAnimalKind.Order) standing at its pen's stand point, with a 3D voice source.</summary>
        static AnimalSet CreateAnimals(ParkLayout p, Transform parent)
        {
            AnimalMaterials.Clear();
            UsedAnimalMaterialPaths.Clear();

            int count = ZooAnimalKind.Order.Length;
            var set = new AnimalSet { Animals = new ZooAnimal[count], Models = new GameObject[count] };
            for (int i = 0; i < count; i++)
            {
                ZooAnimalKind kind = ZooAnimalKind.Order[i];
                Transform stand = i < p.Pens.Count && p.Pens[i].Stand != null ? p.Pens[i].Stand : p.ZonePoint;

                var root = new GameObject("ZooAnimal_" + kind.Id);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(stand.position, stand.rotation);

                GameObject model = MakeAnimal(new PrefabRef(kind.PrefabName, "polyperfect"), root.transform, kind.Height, stand.position);
                if (model != null) model.name = "Model";

                var voice = root.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.spatialBlend = 1f;
                voice.minDistance = 2f;
                voice.maxDistance = 14f;
                voice.rolloffMode = AudioRolloffMode.Linear;

                var animal = root.AddComponent<ZooAnimal>();
                SetString(animal, "kindId", kind.Id);
                SetObject(animal, "animator", model != null ? model.GetComponentInChildren<Animator>() : null);
                SetObject(animal, "model", model != null ? model.transform : null);
                SetFloat(animal, "height", kind.Height);
                SetObject(animal, "voice", voice);

                set.Animals[i] = animal;
                set.Models[i] = model;
            }
            return set;
        }

        /// <summary>The three surprise critters, switched off, parked in a corner of the park near the entrance.</summary>
        static SurpriseCritter[] CreateCritters(ParkLayout p, Transform parent)
        {
            Transform group = NewGroup("Critters", parent);
            var critters = new SurpriseCritter[CritterTable.Length];
            for (int i = 0; i < CritterTable.Length; i++)
            {
                (string id, string prefab, float height) = CritterTable[i];
                Vector3 home = p.At(2f + i * 1.2f, -10f);

                var root = new GameObject("Critter_" + id);
                root.transform.SetParent(group, false);
                root.transform.SetPositionAndRotation(home, Quaternion.LookRotation(Vector3.back, Vector3.up));

                GameObject model = MakeAnimal(new PrefabRef(prefab, "polyperfect"), root.transform, height, home);
                if (model != null) model.name = "Model";

                critters[i] = root.AddComponent<SurpriseCritter>();
                root.SetActive(false);
            }
            return critters;
        }

        /// <summary>
        /// One animal under the parent: unpacked, stripped of everything that is not a mesh or the Animator, with URP materials, scaled
        /// to the wanted height and standing on the point. Returns null when the prefab is missing.
        /// </summary>
        static GameObject MakeAnimal(PrefabRef reference, Transform parent, float height, Vector3 point)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            var animal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(animal, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            animal.name = prefab.name;
            animal.transform.localPosition = Vector3.zero;
            animal.transform.localRotation = Quaternion.identity;
            animal.transform.localScale = Vector3.one;

            // Scripts first (they require the agent, the audio source, and so on), then the rest.
            foreach (MonoBehaviour script in animal.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) Object.DestroyImmediate(script);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(animal);
            foreach (NavMeshAgent agent in animal.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(agent);
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
                    if (own != null && !vendors) source = own;
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

        /// <summary>
        /// A URP/Lit copy of a Built-in Standard material (_MainTex to _BaseMap, _Color to _BaseColor, low smoothness), saved under
        /// Materials/Zoo/Animals. Materials that are already URP are used as they are.
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

        // ---- The zookeeper ----

        /// <summary>
        /// The zookeeper: a classmate in an explorer outfit and a scout hat, built like the knight allies (Kid controller, KidAnimator,
        /// NavMeshAgent), without weapons. A trigger capsule, so the child can walk through her. Switched off until the game starts.
        /// </summary>
        static ZooKeeper CreateKeeper(ParkLayout p, ZooInputs inputs, Transform parent)
        {
            PrefabRef reference = PrefabExists(KeeperPrefab) ? KeeperPrefab : KeeperFallbackPrefab;
            Quaternion facing = p.KeeperHome.rotation;
            p.KeeperHome.position = SnapToNavMesh(p.KeeperHome.position);

            GameObject root = CreateKid("ZooKeeper", reference, inputs.KidController, p.KeeperHome.position, out float height);
            root.transform.SetParent(parent, true);
            root.transform.rotation = facing;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = height;
            agent.angularSpeed = 720f;
            agent.acceleration = 14f;
            agent.speed = 2.8f;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = 0.25f;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            AttachScoutHat(root);

            var keeper = root.AddComponent<ZooKeeper>();
            SetObject(keeper, "agent", agent);
            SetObject(keeper, "kidAnimator", root.GetComponent<KidAnimator>());
            SetObject(keeper, "home", p.KeeperHome);

            SetLayerRecursive(root, inputs.KidsLayer);
            root.SetActive(false);
            return keeper;
        }

        /// <summary>The scout hat on the kid's head bone, fitted by AttachmentFit like the other hats; no colliders.</summary>
        static void AttachScoutHat(GameObject kid)
        {
            GameObject hat = LoadPrefab(ScoutHatPrefab);
            if (hat == null) return;

            Animator animator = kid.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) head = FindDeep(kid.transform, "Head");
            if (head == null)
            {
                Debug.LogWarning("CoolSchool zoo park: the zookeeper has no head bone, so she has no scout hat.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(hat, head);
            AttachmentFit.Place(instance.transform, animator, head, Vector3.zero, Vector3.zero, Vector3.one);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }
    }
}
