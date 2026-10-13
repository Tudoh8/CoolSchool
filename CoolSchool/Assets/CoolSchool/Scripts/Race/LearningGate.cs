using System;
using CoolSchool.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Race
{
    /// <summary>
    /// A learning gate: three arches across the street, one per lane, each with a number or a letter on its board. The
    /// announcer says which one to drive through. The gate only reports which lane the player's cart went through (and
    /// whether that was the right one); the race decides what to say. Nothing here can go wrong for the child: a wrong lane
    /// just lets the right arch flash.
    /// The arch glow renderers need a material with an emission colour (URP/Lit with the emission keyword on); the gate
    /// makes its own copies of those materials and drives their emission.
    /// </summary>
    public class LearningGate : RaceTrackItem
    {
        public enum GateKind { Number, Letter }

        const float RestGlow = 0.25f;
        const float LitGlow = 1.6f;
        const float LitPulse = 0.6f;
        const float LitPulseSpeed = 5f;
        const float FlashGlow = 3.5f;
        const float FlashOffGlow = 0.15f;
        const float FlashStepSeconds = 0.25f;
        const float ShowRightSeconds = 1.5f;
        const float CelebrateFlashSeconds = 1f;
        const float ArchHeight = 1.6f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("The glyph on each arch's board, lane 0..2 (world-space canvas text).")]
        [SerializeField] Text[] glyphs;
        [Tooltip("The glowing strip on each arch, lane 0..2.")]
        [SerializeField] Renderer[] archGlow;

        Material[] materials;
        Color[] colors;
        bool lit;
        float flashTimer;
        float flashLength;

        public GateKind Kind { get; private set; }

        /// <summary>The lane with the right answer (-1 until Setup).</summary>
        public int CorrectLane { get; private set; } = -1;

        /// <summary>True once the race has announced this gate (set by the race).</summary>
        public bool Announced { get; set; }

        /// <summary>True once the player's cart drove through.</summary>
        public bool PlayerPassed { get; private set; }

        /// <summary>The player's cart passed: gate, the lane it went through, and whether that lane was the right one.</summary>
        public event Action<LearningGate, int, bool> Passed;

        /// <summary>The font of the boards (the podium digits borrow it), or null.</summary>
        public Font GlyphFont => glyphs != null && glyphs.Length > 0 && glyphs[0] != null ? glyphs[0].font : null;

        public override bool Blocking => false;

        /// <summary>A gate covers the whole street.</summary>
        public override bool Covers(int laneIndex) => laneIndex >= 0 && laneIndex <= 2;

        void Awake()
        {
            Cache();
        }

        void OnDestroy()
        {
            if (materials == null) return;

            foreach (Material material in materials)
                if (material != null) Destroy(material);
        }

        void Cache()
        {
            if (materials != null) return;

            int count = archGlow != null ? archGlow.Length : 0;
            materials = new Material[count];
            colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                if (archGlow[i] == null) continue;

                Material material = archGlow[i].material;   // our own copy, so a gate never changes the shared asset
                material.EnableKeyword("_EMISSION");
                materials[i] = material;
                colors[i] = ReadColor(material);
            }
            ApplyGlow(RestGlow, -1);
        }

        static Color ReadColor(Material material)
        {
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return Color.white;
        }

        /// <summary>Puts the question on the boards: one option per lane (a digit or a letter), and which lane is right.</summary>
        public void Setup(string[] options, int correctLane, GateKind kind)
        {
            Cache();
            Kind = kind;
            CorrectLane = correctLane;

            if (glyphs == null) return;
            for (int i = 0; i < glyphs.Length; i++)
            {
                if (glyphs[i] == null) continue;
                glyphs[i].text = options != null && i < options.Length ? options[i] : string.Empty;
            }
        }

        /// <summary>The arches glow and pulse (the gate has been announced) or go back to a quiet glow.</summary>
        public void LightUp(bool on)
        {
            Cache();
            lit = on;
            if (flashTimer <= 0f) ApplyGlow(on ? LitGlow : RestGlow, -1);
        }

        /// <summary>The right arch is celebrated: confetti and sparkles in its colour, and a quick flash.</summary>
        public void Celebrate()
        {
            Cache();
            int arch = Mathf.Clamp(CorrectLane, 0, 2);
            Vector3 where = ArchPosition(arch);
            BattleEffects.Confetti(where, ArchColor(arch));
            BattleEffects.Sparkle(where);
            StartFlash(CelebrateFlashSeconds);
        }

        /// <summary>The right arch flashes three times, so he can see where it was.</summary>
        public void ShowRight()
        {
            Cache();
            StartFlash(ShowRightSeconds);
        }

        void StartFlash(float seconds)
        {
            flashLength = seconds;
            flashTimer = seconds;
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f)
                {
                    ApplyGlow(lit ? LitGlow : RestGlow, -1);
                    return;
                }

                int step = Mathf.FloorToInt((flashLength - flashTimer) / FlashStepSeconds);
                float flash = step % 2 == 0 ? FlashGlow : FlashOffGlow;
                ApplyGlow(lit ? LitGlow : RestGlow, Mathf.Clamp(CorrectLane, 0, 2), flash);
                return;
            }

            if (lit) ApplyGlow(LitGlow + Mathf.Sin(Time.time * LitPulseSpeed) * LitPulse, -1);
        }

        /// <summary>Sets the glow of all arches, with one arch (or none, -1) at its own strength.</summary>
        void ApplyGlow(float strength, int specialArch, float specialStrength = 0f)
        {
            if (materials == null) return;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                float value = i == specialArch ? specialStrength : strength;
                materials[i].SetColor(EmissionColorId, colors[i] * value);
            }
        }

        Color ArchColor(int arch)
        {
            if (colors != null && arch >= 0 && arch < colors.Length) return colors[arch];
            return Color.white;
        }

        Vector3 ArchPosition(int arch)
        {
            if (Track != null) return Track.PointAt(Distance, Track.LaneOffset(arch)) + Vector3.up * ArchHeight;
            return transform.position + Vector3.up * ArchHeight;
        }

        public override void OnCartPassing(CartController cart, RaceTrack track)
        {
            if (cart == null || !cart.IsPlayer || PlayerPassed) return;

            // The lane he steered to counts (also if the cart is still sliding over as it goes through).
            int playerLane = Mathf.Clamp(cart.Lane, 0, 2);
            PlayerPassed = true;
            Passed?.Invoke(this, playerLane, playerLane == CorrectLane);
        }

        public override void ResetItem()
        {
            Cache();
            Announced = false;
            PlayerPassed = false;
            lit = false;
            flashTimer = 0f;
            ApplyGlow(RestGlow, -1);
        }
    }
}
