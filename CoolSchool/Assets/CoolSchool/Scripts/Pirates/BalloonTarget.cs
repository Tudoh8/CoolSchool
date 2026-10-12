using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// A balloon on the skeleton boat. It bobs gently in the air and pops into confetti of its own colour when the cannon
    /// ball arrives. The colour is set with a MaterialPropertyBlock, so all balloons can share one material.
    /// </summary>
    public class BalloonTarget : MonoBehaviour
    {
        [SerializeField] Renderer balloon;
        [SerializeField] Color color = new Color(1f, 0.35f, 0.35f, 1f);
        [Tooltip("How much of the colour also glows. 0 = none.")]
        [SerializeField, Range(0f, 1f)] float glow = 0.35f;
        [SerializeField] float bobHeight = 0.07f;
        [SerializeField] float bobSpeed = 2.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock block;
        Transform bobbed;
        Vector3 bobBase;
        float phase;

        public bool IsPopped { get; private set; }

        /// <summary>The middle of the balloon, where a cannon ball flies to.</summary>
        public Vector3 AimPoint => balloon != null ? balloon.bounds.center : transform.position;

        /// <summary>The balloon's colour (confetti, the cannon ball).</summary>
        public Color BalloonColor => color;

        void Awake()
        {
            if (balloon == null) balloon = GetComponentInChildren<Renderer>(true);
            if (balloon != null)
            {
                bobbed = balloon.transform;
                bobBase = bobbed.localPosition;
            }
            phase = (GetInstanceID() & 0xFF) / 255f * Mathf.PI * 2f;
        }

        void OnEnable()
        {
            ApplyColor();
        }

        void Update()
        {
            if (bobbed == null || IsPopped) return;

            bobbed.localPosition = bobBase + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);
        }

        /// <summary>Changes the colour (builder or game).</summary>
        public void SetColor(Color newColor)
        {
            color = newColor;
            ApplyColor();
        }

        void ApplyColor()
        {
            if (balloon == null) return;

            if (block == null) block = new MaterialPropertyBlock();
            balloon.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            block.SetColor(EmissionColorId, color * glow);
            balloon.SetPropertyBlock(block);
        }

        /// <summary>Pop! The balloon disappears in confetti of its own colour.</summary>
        public void Pop()
        {
            if (IsPopped) return;

            IsPopped = true;
            Vector3 where = AimPoint;
            if (balloon != null) balloon.enabled = false;

            BattleEffects.Confetti(where, color);
            BattleEffects.Sparkle(where);
            Sfx.Play("pirates.balloon_pop", where);
        }

        /// <summary>A fresh balloon again, for the next round.</summary>
        public void ResetBalloon()
        {
            IsPopped = false;
            if (balloon == null) return;

            balloon.enabled = true;
            if (bobbed != null) bobbed.localPosition = bobBase;
            ApplyColor();
        }
    }
}
