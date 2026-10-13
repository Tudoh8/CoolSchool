using System;
using System.Collections.Generic;
using CoolSchool.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Race
{
    /// <summary>The places on a cart body that an add-on or a plate can be fixed to. Each body stores one point for each.</summary>
    public enum CartAnchor
    {
        /// <summary>On the nose, facing forward, about two thirds up: the googly eyes.</summary>
        Front,
        /// <summary>On the tail, about half way up: where the balloon strings are tied.</summary>
        Back,
        /// <summary>The top surface behind the seat: the propeller.</summary>
        Top,
        /// <summary>The top edge of the tail: the spoiler.</summary>
        Spoiler,
        /// <summary>The foot of the flag pole, near the tail.</summary>
        FlagFoot,
        /// <summary>The middle of the nose, low: the front number plate.</summary>
        PlateFront,
        /// <summary>The middle of the tail, low: the back number plate.</summary>
        PlateBack
    }

    /// <summary>How an add-on is fixed to the body.</summary>
    public enum AddonPlacement
    {
        /// <summary>The add-on's root sits on one anchor point (its own origin is that point, it faces forward).</summary>
        AtAnchor,
        /// <summary>The root has four child sprites (left front, left back, right front, right back) that are laid on the body's sides.</summary>
        SideStickers
    }

    /// <summary>
    /// What a soapbox cart looks like: which body is shown (and where the kid and his buddy sit on it), its paint, the add-ons that
    /// are on, the trail it leaves, and the number on its plates. Everything is picked by item id from a <see cref="CartChoice"/>, and
    /// the lists below are filled by the race builder from the reward catalog, so a new body, paint, add-on or trail is one more entry
    /// there (the builder's CartGarageData) and nothing in this class. The player's cart takes the looks he picked; the rivals get a
    /// random choice (<see cref="RandomChoice"/>).
    /// </summary>
    public class CartLooks : MonoBehaviour
    {
        public const string FlagId = "cart.flag";
        public const string StickersId = "cart.stickers";
        public const string GoldId = ProgressStore.GoldPaintId;

        /// <summary>One cart body: its model (a child of the cart's Model, only the shown one is active) and where things go on it.</summary>
        [Serializable]
        public class BodyEntry
        {
            [Tooltip("The catalog item id of the body, for example cart.racer_1.")]
            public string id;
            public GameObject root;
            [Tooltip("Where the kid's seat is, in the Model's space.")]
            public Vector3 seat;
            [Tooltip("Where a riding buddy sits, in the Model's space.")]
            public Vector3 buddySeat;
            [Tooltip("The body's bounds in the Model's space (the add-on anchors are worked out from them when an anchor is not stored).")]
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
            [Tooltip("One point per CartAnchor, in the Model's space. A point that is exactly zero is not stored and is worked out from the bounds.")]
            public Vector3[] anchors;
            [Tooltip("Scales the add-ons that scale with the body (eyes, balloons...): 1 is a body about 0.9 m wide.")]
            public float addonScale = 1f;
        }

        /// <summary>One add-on (flag, stickers, eyes...): switched on or off as a whole.</summary>
        [Serializable]
        public class AddonEntry
        {
            public string id;
            public GameObject root;
            public CartAnchor anchor;
            public AddonPlacement placement;
            [Tooltip("Scaled by the body's addonScale (true for the things that must fit the body; false for the flag pole).")]
            public bool scaleWithBody = true;
        }

        /// <summary>One trail: a root with a TrailRenderer or particle systems below it, switched on while the cart moves.</summary>
        [Serializable]
        public class TrailEntry
        {
            public string id;
            public GameObject root;
        }

        /// <summary>A body material and the painted copy of it.</summary>
        [Serializable]
        public class MaterialSwap
        {
            public Material from;
            public Material to;
        }

        /// <summary>One paint: either one material for the whole body (gold) or a painted copy of each body material.</summary>
        [Serializable]
        public class PaintEntry
        {
            public string id;
            [Tooltip("Replaces every material of the body (gold). Empty for a recoloured paint.")]
            public Material whole;
            public MaterialSwap[] swaps;
        }

        [Header("Looks (filled by the race builder from the reward catalog)")]
        [SerializeField] BodyEntry[] bodies;
        [SerializeField] AddonEntry[] addons;
        [SerializeField] PaintEntry[] paints;
        [SerializeField] TrailEntry[] trails;

        [Header("Parts that move with the body")]
        [Tooltip("The kid's seat (the same object the CartController has). Moved to the shown body's seat point.")]
        [SerializeField] Transform seat;
        [Tooltip("The buddy's seat. Moved to the shown body's buddy seat point.")]
        [SerializeField] Transform buddySeat;
        [SerializeField] Transform plateFront;
        [SerializeField] Transform plateBack;
        [Tooltip("The digits on the front and back plate.")]
        [SerializeField] Text plateFrontText;
        [SerializeField] Text plateBackText;

        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        bool cached;
        PaintEntry paint;
        CartController cart;
        Vector3 lastPosition;
        bool trailMoving;
        TrailRenderer[] activeTrailRenderers = new TrailRenderer[0];
        ParticleSystem[] activeTrailSystems = new ParticleSystem[0];

        /// <summary>The body that is shown now (its index in the list of bodies).</summary>
        public int BodyIndex { get; private set; }

        /// <summary>The id of the body that is shown now.</summary>
        public string BodyId => bodies != null && BodyIndex < bodies.Length && bodies[BodyIndex] != null ? bodies[BodyIndex].id : "";

        /// <summary>How many bodies this cart has a model for.</summary>
        public int BodyCount => bodies != null ? bodies.Length : 0;

        void Awake()
        {
            cart = GetComponent<CartController>();
            lastPosition = transform.position;
            CacheOriginals();
        }

        // ----- Putting a whole look on --------------------------------------------------------------

        /// <summary>
        /// Shows this choice. With ownedOnly the add-ons that are not owned stay off (the player's cart); the rivals show whatever
        /// their random choice says.
        /// </summary>
        public void Apply(CartChoice choice, bool ownedOnly)
        {
            if (choice == null) return;

            SetBody(choice.bodyId);
            SetPaint(choice.paintId);
            if (addons != null)
            {
                PlayerProgress progress = ownedOnly ? ProgressStore.Current : null;
                foreach (AddonEntry addon in addons)
                {
                    if (addon == null || addon.root == null) continue;

                    bool on = choice.AddonOn(addon.id) && (!ownedOnly || (progress != null && progress.IsUnlocked(addon.id)));
                    addon.root.SetActive(on);
                }
            }
            SetTrail(choice.trailId);
            SetNumber(choice.number);
        }

        /// <summary>The player's cart as he chose it: the saved choice, with only the add-ons he owns.</summary>
        public void ApplyPlayerLooks(PlayerProgress progress)
        {
            Apply(ProgressStore.LoadCartChoice(), true);
        }

        // ----- Body ---------------------------------------------------------------------------------

        public bool HasBody(string id)
        {
            return IndexOfBody(id) >= 0;
        }

        public string BodyIdAt(int index)
        {
            return bodies != null && index >= 0 && index < bodies.Length && bodies[index] != null ? bodies[index].id : "";
        }

        /// <summary>Shows the body with this id (the first body when the id is unknown) and puts the kid's seat, the buddy's seat, the add-ons and the plates on it.</summary>
        public void SetBody(string id)
        {
            ShowBody(Mathf.Max(0, IndexOfBody(id)));
        }

        /// <summary>Shows body number index (clamped). Used by the preview shots.</summary>
        public void SetBody(int index)
        {
            if (bodies == null || bodies.Length == 0) return;
            ShowBody(Mathf.Clamp(index, 0, bodies.Length - 1));
        }

        int IndexOfBody(string id)
        {
            if (bodies == null || string.IsNullOrEmpty(id)) return -1;

            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] != null && bodies[i].id == id) return i;
            return -1;
        }

        void ShowBody(int index)
        {
            if (bodies == null || bodies.Length == 0) return;

            BodyIndex = index;
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] != null && bodies[i].root != null) bodies[i].root.SetActive(i == index);

            BodyEntry body = bodies[index];
            if (body != null)
            {
                if (seat != null) seat.localPosition = body.seat;
                if (buddySeat != null) buddySeat.localPosition = body.buddySeat;
                PlaceAddons(body);
            }

            // The shown body gets the paint too.
            ApplyPaint();
        }

        // ----- Add-ons and plates -------------------------------------------------------------------

        /// <summary>Switches one add-on on or off (the preview shots and the old callers; the race uses Apply).</summary>
        public void SetAddon(string id, bool on)
        {
            if (addons == null) return;

            foreach (AddonEntry addon in addons)
                if (addon != null && addon.root != null && addon.id == id) addon.root.SetActive(on);
        }

        /// <summary>The old three extras: the flag, the stickers and the gold paint (the preview shots use it).</summary>
        public void SetExtras(bool showFlag, bool showStickers, bool showGold)
        {
            SetAddon(FlagId, showFlag);
            SetAddon(StickersId, showStickers);
            SetPaint(showGold ? GoldId : "");
        }

        void PlaceAddons(BodyEntry body)
        {
            if (addons != null)
            {
                foreach (AddonEntry addon in addons)
                {
                    if (addon == null || addon.root == null) continue;

                    Transform root = addon.root.transform;
                    if (addon.placement == AddonPlacement.SideStickers)
                    {
                        PlaceStickers(root, body);
                        continue;
                    }
                    root.localPosition = AnchorOf(body, addon.anchor);
                    root.localScale = Vector3.one * (addon.scaleWithBody ? Mathf.Max(0.2f, body.addonScale) : 1f);
                }
            }

            if (plateFront != null) plateFront.localPosition = AnchorOf(body, CartAnchor.PlateFront);
            if (plateBack != null) plateBack.localPosition = AnchorOf(body, CartAnchor.PlateBack);
        }

        // The sticker sprites: left side then right side, each at 38% and 68% of the length from the tail. Same spots as the builder's.
        static readonly float[] StickerSide = { -1f, -1f, 1f, 1f };
        static readonly float[] StickerAlong = { 0.38f, 0.68f, 0.38f, 0.68f };

        static void PlaceStickers(Transform group, BodyEntry body)
        {
            Vector3 center = body.boundsCenter;
            Vector3 size = body.boundsSize;
            float y = center.y - size.y * 0.5f + size.y * 0.55f;
            for (int i = 0; i < group.childCount && i < StickerSide.Length; i++)
            {
                float z = Mathf.Lerp(center.z - size.z * 0.5f, center.z + size.z * 0.5f, StickerAlong[i]);
                group.GetChild(i).localPosition = new Vector3(center.x + StickerSide[i] * (size.x * 0.5f + 0.012f), y, z);
            }
        }

        /// <summary>The stored anchor point of a body, or one worked out from its bounds when none is stored.</summary>
        public static Vector3 AnchorOf(BodyEntry body, CartAnchor anchor)
        {
            int index = (int)anchor;
            if (body.anchors != null && index < body.anchors.Length && body.anchors[index] != Vector3.zero)
                return body.anchors[index];

            Vector3 center = body.boundsCenter;
            Vector3 size = body.boundsSize;
            float minY = center.y - size.y * 0.5f;
            float minZ = center.z - size.z * 0.5f;
            float maxZ = center.z + size.z * 0.5f;
            switch (anchor)
            {
                case CartAnchor.Front: return new Vector3(center.x, minY + size.y * 0.65f, maxZ);
                case CartAnchor.Back: return new Vector3(center.x, minY + size.y * 0.55f, minZ);
                case CartAnchor.Top: return new Vector3(center.x, minY + size.y * 0.8f, body.seat.z - 0.4f);
                case CartAnchor.Spoiler: return new Vector3(center.x, minY + size.y * 0.75f, minZ + 0.1f);
                case CartAnchor.FlagFoot: return new Vector3(center.x + size.x * 0.25f, minY + size.y * 0.7f, minZ + 0.12f);
                case CartAnchor.PlateFront: return new Vector3(center.x, minY + size.y * 0.3f, maxZ);
                default: return new Vector3(center.x, minY + size.y * 0.3f, minZ);
            }
        }

        // ----- Number plate -------------------------------------------------------------------------

        /// <summary>The digits on both plates (1 to 10).</summary>
        public void SetNumber(int number)
        {
            string digits = Mathf.Clamp(number, CartChoice.MinNumber, CartChoice.MaxNumber).ToString();
            if (plateFrontText != null) plateFrontText.text = digits;
            if (plateBackText != null) plateBackText.text = digits;
        }

        // ----- Paint --------------------------------------------------------------------------------

        /// <summary>Paints the body with this paint item id ("" or an unknown id gives the body's own colours).</summary>
        public void SetPaint(string id)
        {
            paint = null;
            if (paints != null && !string.IsNullOrEmpty(id))
            {
                foreach (PaintEntry entry in paints)
                {
                    if (entry != null && entry.id == id)
                    {
                        paint = entry;
                        break;
                    }
                }
            }
            ApplyPaint();
        }

        void CacheOriginals()
        {
            if (cached) return;
            cached = true;

            if (bodies == null) return;
            foreach (BodyEntry body in bodies)
            {
                if (body == null || body.root == null) continue;
                foreach (Renderer renderer in body.root.GetComponentsInChildren<Renderer>(true))
                    originalMaterials[renderer] = renderer.sharedMaterials;
            }
        }

        void ApplyPaint()
        {
            CacheOriginals();

            foreach (KeyValuePair<Renderer, Material[]> entry in originalMaterials)
            {
                Renderer renderer = entry.Key;
                if (renderer == null) continue;

                if (paint == null)
                {
                    renderer.sharedMaterials = entry.Value;
                    continue;
                }

                var painted = new Material[entry.Value.Length];
                for (int i = 0; i < painted.Length; i++)
                    painted[i] = paint.whole != null ? paint.whole : SwapFor(paint, entry.Value[i]);
                renderer.sharedMaterials = painted;
            }
        }

        /// <summary>The painted copy of a body material; the material itself when this paint has none for it.</summary>
        static Material SwapFor(PaintEntry paint, Material original)
        {
            if (paint.swaps != null)
            {
                foreach (MaterialSwap swap in paint.swaps)
                    if (swap != null && swap.from == original && swap.to != null) return swap.to;
            }
            return original;
        }

        // ----- Trail --------------------------------------------------------------------------------

        /// <summary>Switches the trail with this id on and every other trail off ("" = none). The trail only emits while the cart moves.</summary>
        public void SetTrail(string id)
        {
            var renderers = new List<TrailRenderer>();
            var systems = new List<ParticleSystem>();
            if (trails != null)
            {
                foreach (TrailEntry trail in trails)
                {
                    if (trail == null || trail.root == null) continue;

                    bool on = !string.IsNullOrEmpty(id) && trail.id == id;
                    if (trail.root.activeSelf != on) trail.root.SetActive(on);
                    if (!on) continue;

                    renderers.AddRange(trail.root.GetComponentsInChildren<TrailRenderer>(true));
                    systems.AddRange(trail.root.GetComponentsInChildren<ParticleSystem>(true));
                }
            }
            activeTrailRenderers = renderers.ToArray();
            activeTrailSystems = systems.ToArray();
            trailMoving = true;
            SetTrailEmitting(false);
        }

        void Update()
        {
            if (activeTrailRenderers.Length == 0 && activeTrailSystems.Length == 0) return;

            // A jump of the whole cart (back to the start line) would draw a streak across the street: wipe the ribbons.
            Vector3 position = transform.position;
            if ((position - lastPosition).sqrMagnitude > 9f)
                foreach (TrailRenderer ribbon in activeTrailRenderers)
                    if (ribbon != null) ribbon.Clear();
            lastPosition = position;

            bool moving = cart != null && cart.Speed > 0.5f;
            if (moving != trailMoving) SetTrailEmitting(moving);
        }

        void SetTrailEmitting(bool moving)
        {
            trailMoving = moving;
            foreach (TrailRenderer ribbon in activeTrailRenderers)
            {
                if (ribbon == null) continue;
                if (moving && !ribbon.emitting) ribbon.Clear();
                ribbon.emitting = moving;
            }
            foreach (ParticleSystem system in activeTrailSystems)
            {
                if (system == null) continue;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = moving;
            }
        }

        // ----- Random looks for the rivals ----------------------------------------------------------

        /// <summary>
        /// A random look from everything this cart has (owned or not, so he sees what can be won): any body that is not in
        /// <paramref name="avoidBodies"/>, any paint or none, 0 to 2 add-ons, no trail, and a number that is not in
        /// <paramref name="avoidNumbers"/>. The add-ons that are not picked are listed as off.
        /// </summary>
        public CartChoice RandomChoice(ICollection<string> avoidBodies, ICollection<int> avoidNumbers)
        {
            var choice = new CartChoice();

            var candidates = new List<string>();
            if (bodies != null)
            {
                foreach (BodyEntry body in bodies)
                    if (body != null && !string.IsNullOrEmpty(body.id) && (avoidBodies == null || !avoidBodies.Contains(body.id))) candidates.Add(body.id);
                if (candidates.Count == 0)
                    foreach (BodyEntry body in bodies)
                        if (body != null && !string.IsNullOrEmpty(body.id)) candidates.Add(body.id);
            }
            if (candidates.Count > 0) choice.bodyId = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            if (paints != null && paints.Length > 0)
            {
                int pick = UnityEngine.Random.Range(0, paints.Length + 1);
                choice.paintId = pick == 0 || paints[pick - 1] == null ? "" : paints[pick - 1].id;
            }

            var addonIds = new List<string>();
            if (addons != null)
                foreach (AddonEntry addon in addons)
                    if (addon != null && !string.IsNullOrEmpty(addon.id)) addonIds.Add(addon.id);
            for (int i = addonIds.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (addonIds[i], addonIds[j]) = (addonIds[j], addonIds[i]);
            }
            int wanted = UnityEngine.Random.Range(0, 3);
            for (int i = 0; i < addonIds.Count; i++)
                if (i >= wanted) choice.addonsOff.Add(addonIds[i]);

            for (int attempt = 0; attempt < 30; attempt++)
            {
                choice.number = UnityEngine.Random.Range(CartChoice.MinNumber, CartChoice.MaxNumber + 1);
                if (avoidNumbers == null || !avoidNumbers.Contains(choice.number)) break;
            }
            return choice;
        }
    }
}
