using System.Linq;
using CoolSchool.Characters;
using CoolSchool.Shop;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the five ice cream shop customers (inactive, ready for IceCreamShop): two kids, a knight, an alien
    /// and a small friendly dragon. They are separate new instances, not the playground's roaming kids, and they have
    /// no colliders and no NavMeshAgent: ShopCustomer walks them by transform.
    /// Each gets a BubbleAnchor just above its head and a HandPoint for the cone (the right hand, or in front of the
    /// dragon's mouth).
    /// </summary>
    internal static class ShopCustomerBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string KidControllerPath = RootFolder + "/Animation/Kid.controller";
        const string DragonControllerPath = RootFolder + "/Animation/Dragon.controller";
        const string KidsLayerName = "Kids";

        // The kid models are none of the player's (schoolboy) or the playground kids' (hoodie, schoolgirl, superhero, pirate, dino).
        static readonly PrefabRef KidPrefab = Kids("SM_Chr_Kid_Dress_01");
        static readonly PrefabRef KnightPrefab = Kids("SM_Chr_Kid_Knight_01");
        static readonly PrefabRef SecondKidPrefab = Kids("SM_Chr_Kid_Onesie_Bunny_01");
        static readonly PrefabRef AlienPrefab = Kids("SM_Chr_Kid_Alien_02");
        static readonly PrefabRef DragonPrefab = new PrefabRef("Dragon_LP", "Magic Pig");

        /// <summary>How far above the top of the head (renderer bounds) the bubble anchor floats.</summary>
        const float BubbleMargin = 0.35f;

        // Kids' hand: the palm sits this far from the wrist bone along the forearm, and the cone is held a bit in front.
        // These are guesses: tune them on the ShopCustomer components.
        const float PalmReach = 0.05f;
        static readonly Vector3 KidHandRootOffset = new Vector3(0f, 0.02f, 0.1f);

        // The dragon: a small friendly one. The wanted shoulder height decides the size, within sane limits for the
        // whole model's height (the shop camera must still see the bubble above its head).
        const float DragonShoulderHeight = 1.6f;
        const float DragonMinHeight = 2.2f;
        const float DragonMaxHeight = 3f;
        const float DragonFallbackHeight = 2.6f;
        /// <summary>How far in front of the jaw bone the cone floats, and how far below it.</summary>
        const float MouthReach = 0.6f;
        const float MouthDrop = 0.05f;

        /// <summary>
        /// Builds the five customers under parent, inactive, in visit order:
        /// [0] Kid, [1] Knight, [2] Kid (a different model from [0]), [3] Alien, [4] Dragon.
        /// </summary>
        internal static ShopCustomer[] BuildCustomers(Transform parent)
        {
            var kidController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KidControllerPath);
            if (kidController == null)
                Debug.LogWarning("CoolSchool: Kid.controller not found; the shop customers will stand still.");

            int layer = LayerMask.NameToLayer(KidsLayerName);

            return new[]
            {
                BuildKidLike(parent, CustomerKind.Kid, KidPrefab, kidController, layer),
                BuildKidLike(parent, CustomerKind.Knight, KnightPrefab, kidController, layer),
                BuildKidLike(parent, CustomerKind.Kid, SecondKidPrefab, kidController, layer),
                BuildKidLike(parent, CustomerKind.Alien, AlienPrefab, kidController, layer),
                BuildDragon(parent, layer),
            };
        }

        // ----- Kids, the knight and the alien --------------------------------------------------

        /// <summary>A kid-rig customer: the Synty model with the Kid controller and KidAnimator, as the playground kids have.</summary>
        static ShopCustomer BuildKidLike(Transform parent, CustomerKind kind, PrefabRef prefab, RuntimeAnimatorController controller, int layer)
        {
            // Built at the origin, where bounds are easy to read; the root moves under parent at the end.
            GameObject root = CreateKid("Customer_" + prefab.Name.Replace("SM_Chr_Kid_", ""), prefab, controller, Vector3.zero, out float height);
            DisableColliders(root);

            Animator animator = root.GetComponentInChildren<Animator>();
            var customer = root.AddComponent<ShopCustomer>();
            SetEnum(customer, "kind", (int)kind);
            SetObject(customer, "animator", animator);
            SetObject(customer, "kidAnimator", root.GetComponent<KidAnimator>());
            SetObject(customer, "body", animator != null ? animator.transform : null);

            float top = TryGetBounds(root, out Bounds bounds) ? bounds.max.y : height;
            Transform bubble = NewPoint("BubbleAnchor", root.transform, new Vector3(0f, top + BubbleMargin, 0f));
            SetObject(customer, "bubbleAnchor", bubble);

            // The hand point follows the right hand bone at runtime. Without a humanoid rig it hangs in front of the chest.
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                // The palm lies a little past the wrist bone, in the direction of the forearm. That is the same in any pose.
                Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Vector3 along = forearm != null ? (hand.position - forearm.position).normalized : Vector3.down;
                Vector3 palm = hand.position + along * PalmReach;

                Transform handPoint = NewPoint("HandPoint", root.transform, root.transform.InverseTransformPoint(palm));
                SetObject(customer, "handPoint", handPoint);
                SetObject(customer, "handBone", hand);
                SetVector3(customer, "handBoneOffset", hand.InverseTransformPoint(palm));
                SetVector3(customer, "handRootOffset", KidHandRootOffset);
            }
            else
            {
                Transform handPoint = NewPoint("HandPoint", root.transform, new Vector3(0.2f, height * 0.55f, 0.25f));
                SetObject(customer, "handPoint", handPoint);
            }

            return Finish(root, customer, parent, layer);
        }

        // ----- The dragon ----------------------------------------------------------------------

        /// <summary>
        /// A small friendly dragon: the Dragon_LP model (vendor scripts stripped, as for the boss) with Dragon.controller,
        /// scaled so its shoulders are about DragonShoulderHeight. The cone floats in front of its mouth.
        /// </summary>
        static ShopCustomer BuildDragon(Transform parent, int layer)
        {
            var root = new GameObject("Customer_Dragon");
            Animator animator = null;
            Transform head = null;
            Transform mouthBone = null;

            GameObject prefab = LoadPrefab(DragonPrefab);
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                KnightsArenaBuilder.StripForeignScripts(model);

                animator = model.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    var dragonController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DragonControllerPath);
                    if (dragonController != null) animator.runtimeAnimatorController = dragonController;
                    else Debug.LogWarning("CoolSchool: Dragon.controller not found; the shop dragon keeps its own animator.");
                }

                mouthBone = KnightsArenaBuilder.FindMouthBone(model);
                KnightsArenaBuilder.FaceModelForward(model, mouthBone);
                ScaleDragon(model);
                AlignToBounds(model, 0f, 0f, 0f, false);

                // Bones move with the model, so look them up after it is scaled and placed.
                head = model.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.EndsWith("Head") && t.GetComponent<Renderer>() == null);
                if (head == null) head = mouthBone;
            }

            DisableColliders(root);

            var customer = root.AddComponent<ShopCustomer>();
            SetEnum(customer, "kind", (int)CustomerKind.Dragon);
            SetObject(customer, "animator", animator);
            SetObject(customer, "body", animator != null ? animator.transform : null);

            float top = TryGetBounds(root, out Bounds bounds) ? bounds.max.y : DragonFallbackHeight;
            Transform bubble = NewPoint("BubbleAnchor", root.transform, new Vector3(0f, top + BubbleMargin, 0f));
            SetObject(customer, "bubbleAnchor", bubble);

            // The cone floats just in front of (and a little below) the mouth, and turns with the head.
            Transform anchorBone = head != null ? head : mouthBone;
            Vector3 mouth = mouthBone != null
                ? mouthBone.position + root.transform.forward * MouthReach + Vector3.down * MouthDrop
                : new Vector3(0f, top * 0.55f, 0.8f);
            Transform handPoint = NewPoint("HandPoint", root.transform, root.transform.InverseTransformPoint(mouth));
            SetObject(customer, "handPoint", handPoint);
            if (anchorBone != null)
            {
                SetObject(customer, "handBone", anchorBone);
                SetVector3(customer, "handBoneOffset", anchorBone.InverseTransformPoint(mouth));
            }

            return Finish(root, customer, parent, layer);
        }

        /// <summary>
        /// Scales the model so the shoulders (the arm collarbone bones) end up at DragonShoulderHeight,
        /// keeping the whole height between the limits. Without a collarbone it gets a fixed height.
        /// </summary>
        static void ScaleDragon(GameObject model)
        {
            if (!TryGetBounds(model, out Bounds measured) || measured.size.y < 0.01f) return;

            float wanted = DragonFallbackHeight;
            Transform shoulder = model.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.EndsWith("ArmCollarbone") && t.GetComponent<Renderer>() == null);
            if (shoulder != null)
            {
                float ratio = (shoulder.position.y - measured.min.y) / measured.size.y;
                if (ratio > 0.25f && ratio < 0.9f)
                    wanted = Mathf.Clamp(DragonShoulderHeight / ratio, DragonMinHeight, DragonMaxHeight);
            }

            model.transform.localScale *= wanted / measured.size.y;
            Debug.Log($"CoolSchool: the shop dragon is scaled to about {wanted:0.0} m tall.");
        }

        // ----- Shared --------------------------------------------------------------------------

        /// <summary>Parents the customer, puts it on the Kids layer and leaves it switched off.</summary>
        static ShopCustomer Finish(GameObject root, ShopCustomer customer, Transform parent, int layer)
        {
            if (parent != null) root.transform.SetParent(parent, false);
            SetLayerRecursive(root, layer);
            root.SetActive(false);
            EditorUtility.SetDirty(customer);
            return customer;
        }

        /// <summary>Customers must never block the player or a tap. Disabling (not removing) also works on prefab instances.</summary>
        static void DisableColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        /// <summary>An empty point under the parent. The parent sits at the origin, unrotated, while building.</summary>
        static Transform NewPoint(string name, Transform parent, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            return point;
        }
    }
}
